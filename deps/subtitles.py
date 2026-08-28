#!/usr/bin/env python3

import argparse
import json
import os
import sys
import time
from pathlib import Path

import faster_whisper
from faster_whisper import WhisperModel

LANGUAGE_CODES = ("ja", "en", "zh", "ko", "es", "fr", "de", "it", "pt", "ru",
                  "ar", "th", "vi")

MIN_EMIT_INTERVAL = 0.5  # seconds


def emit(obj):
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def add_nvidia_library_directories():
    try:
        import site
    except Exception:
        return

    for root in site.getsitepackages():
        for sub in ("nvidia/cublas/bin", "nvidia/cudnn/bin"):
            directory = Path(root) / sub
            if not directory.is_dir():
                continue
            try:
                os.add_dll_directory(str(directory))
            except (AttributeError, OSError):
                pass
            os.environ["PATH"] = str(directory) + os.pathsep + os.environ.get("PATH", "")


def cuda_usable():
    try:
        import ctranslate2
        return ctranslate2.get_cuda_device_count() > 0
    except Exception:
        return False


def create_model(size, device):
    if device == "cpu":
        emit({"notice": "Using CPU (int8)"})
        return WhisperModel(size, device="cpu", compute_type="int8"), "cpu"

    if not cuda_usable():
        emit({"notice": "CUDA is not available - using CPU (int8)"})
        return WhisperModel(size, device="cpu", compute_type="int8"), "cpu"

    try:
        emit({"notice": "Using CUDA (float16)"})
        return WhisperModel(size, device="cuda", compute_type="float16"), "cuda"
    except Exception as ex:
        emit({"notice": "CUDA failed to load ({0}) - using CPU (int8)".format(type(ex).__name__)})
        return WhisperModel(size, device="cpu", compute_type="int8"), "cpu"


def format_timestamp(seconds, srt):
    total = max(0, int(round(seconds * 1000)))
    hours, total = divmod(total, 3600 * 1000)
    minutes, total = divmod(total, 60 * 1000)
    seconds, millis = divmod(total, 1000)
    if srt:
        return "{0:02d}:{1:02d}:{2:02d},{3:03d}".format(hours, minutes, seconds, millis)
    return "{0:02d}:{1:02d}:{2:02d}.{3:03d}".format(hours, minutes, seconds, millis)


def write_subtitle(filename, entries, kind):
    with open(filename, "w", encoding="utf-8") as handle:
        if kind == "vtt":
            handle.write("WEBVTT\n\n")

        index = 1
        for start, end, text in entries:
            if kind == "txt":
                handle.write(text + "\n")
                continue
            if kind == "srt":
                handle.write(str(index) + "\n")
                handle.write("{0} --> {1}\n".format(
                    format_timestamp(start, True), format_timestamp(end, True)))
            else:
                handle.write("{0} --> {1}\n".format(
                    format_timestamp(start, False), format_timestamp(end, False)))
            handle.write(text.rstrip() + "\n\n")
            index += 1


def transcribe_one(model, entry, language, kind):
    input_path = Path(entry["input"])
    output_path = Path(entry["output"])
    target = entry["input"]

    if not input_path.exists():
        raise FileNotFoundError(str(input_path))
    if not output_path.parent.exists():
        output_path.parent.mkdir(parents=True)

    emit({"input": target, "status": "start"})

    segments, info = model.transcribe(
        str(input_path),
        language=None if language == "auto" else language,
        vad_filter=True,
        beam_size=5,
    )

    collected = []
    last_emit = time.time()
    duration = info.duration or 0
    for segment in segments:
        text = (segment.text or "").strip()
        if text:
            collected.append((segment.start, segment.end, text))
        now = time.time()
        if duration > 0 and now - last_emit >= MIN_EMIT_INTERVAL:
            emit({"input": target, "progress": min(1.0, segment.end / duration)})
            last_emit = now

    if not collected:
        emit({"input": target, "progress": 1.0})

    write_subtitle(str(output_path), collected, kind)
    emit({"input": target, "output": str(output_path), "ok": True,
          "empty": not collected})


def looks_like_cuda_failure(ex):
    message = str(ex).lower()
    return "cuda" in message or "cublas" in message or "cudnn" in message or "dll" in message


def run_manifest(model_size, device, entries, language, kind):
    model, used_device = create_model(model_size, device)
    cuda_dropped = False

    for entry in entries:
        target = entry["input"]
        try:
            transcribe_one(model, entry, language, kind)
        except Exception as ex:
            if used_device == "cuda" and not cuda_dropped and looks_like_cuda_failure(ex):
                cuda_dropped = True
                emit({"notice": "CUDA failed during transcription ({0}) - retrying on the CPU".format(type(ex).__name__)})
                model, used_device = create_model(model_size, "cpu")
                try:
                    transcribe_one(model, entry, language, kind)
                except Exception as retry_ex:
                    emit({"input": target, "ok": False,
                          "error": "{0}: {1}".format(type(retry_ex).__name__, retry_ex)})
            else:
                emit({"input": target, "ok": False,
                      "error": "{0}: {1}".format(type(ex).__name__, ex)})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", default="small",
                        help="tiny / base / small / medium / large-v3 / turbo")
    parser.add_argument("--device", default="auto", choices=["auto", "cpu"],
                        help="auto picks CUDA when available")
    parser.add_argument("--language", default="auto",
                        choices=["auto", *LANGUAGE_CODES],
                        help="speech language, or auto to detect")
    parser.add_argument("--format", default="srt", choices=["srt", "vtt", "txt"])
    parser.add_argument("--manifest", help="JSON file with a list of {input, output}")
    args = parser.parse_args()

    manifest_path = Path(args.manifest)
    if not manifest_path.exists():
        sys.stderr.write("Manifest not found: {0}\n".format(manifest_path))
        sys.exit(1)

    with open(manifest_path, encoding="utf-8") as handle:
        entries = json.load(handle)
    if not entries:
        sys.exit(0)

    add_nvidia_library_directories()

    try:
        run_manifest(args.model, args.device, entries, args.language, args.format)
    except Exception as ex:
        message = "Failed to load the Whisper model: {0}: {1}".format(type(ex).__name__, ex)
        for entry in entries:
            emit({"input": entry["input"], "ok": False, "error": message})


if __name__ == "__main__":
    main()
