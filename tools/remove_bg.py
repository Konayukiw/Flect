#!/usr/bin/env python3

import argparse
import json
import sys
from pathlib import Path

def pick_providers(preferred: str):
    if preferred == "cpu":
        return ["CPUExecutionProvider"]
    try:
        import onnxruntime as ort
        available = set(ort.get_available_providers())
        candidates = []
        for p in ["CUDAExecutionProvider", "DmlExecutionProvider", "CPUExecutionProvider"]:
            if p in available:
                candidates.append(p)
        return candidates if candidates else ["CPUExecutionProvider"]
    except Exception:
        return ["CPUExecutionProvider"]

def process_batch(items, model, providers):
    from rembg import new_session, remove
    from PIL import Image

    session = new_session(model, providers=providers)

    for entry in items:
        inp = entry["input"]
        out = entry["output"]
        try:
            with open(inp, "rb") as f:
                data = f.read()
            result = remove(data, session=session)
            Path(out).parent.mkdir(parents=True, exist_ok=True)
            with open(out, "wb") as f:
                f.write(result)
            sys.stdout.write(json.dumps({"input": inp, "output": out, "ok": True}, ensure_ascii=False) + "\n")
        except Exception as e:
            sys.stdout.write(json.dumps({"input": inp, "ok": False, "error": str(e)}, ensure_ascii=False) + "\n")
        sys.stdout.flush()

def process_single(inp, out, model, providers):
    process_batch([{"input": inp, "output": out}], model, providers)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", default="u2net", help="rembg model: u2net / u2net_human_seg / isnet-general-use")
    parser.add_argument("--providers", default="auto", choices=["auto", "cpu"], help="execution provider selection")
    parser.add_argument("--manifest", help="JSON file with list of {input,output}")
    parser.add_argument("--input", help="single input file")
    parser.add_argument("--output", help="single output file")
    args = parser.parse_args()

    providers = pick_providers(args.providers)

    if args.manifest:
        manifest_path = Path(args.manifest)
        if not manifest_path.exists():
            sys.stderr.write(f"Manifest not found: {manifest_path}\n")
            sys.exit(2)
        with open(manifest_path, "r", encoding="utf-8") as f:
            items = json.load(f)
        if not isinstance(items, list):
            sys.stderr.write("Manifest JSON must be a list\n")
            sys.exit(2)
        process_batch(items, args.model, providers)
    elif args.input and args.output:
        process_single(args.input, args.output, args.model, providers)
    else:
        parser.print_help()
        sys.exit(2)

if __name__ == "__main__":
    main()
