# Flect

An Explorer context-menu extension for Windows. Right-click files or folders and edit them without leaving Explorer.

## Installation

Run `Flect-Setup-(version).exe`. Then right-click a file or folder and choose **Show more options** (or press `Shift+F10`) to reach the menu.

Nothing needs administrator rights: it installs to `%LOCALAPPDATA%\Flect` and registers under `HKCU`. It appears in **Settings › Apps** for removal.

Requires the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0). 
Setup checks for it and leads to the download page if it is missing.

Explorer loads the handler into itself and keeps it open, so installing over a version that has already been used restarts Explorer once. Setup does that on its own, and only when the file is genuinely locked.

## How to build

```powershell
.\fetch\get.ps1           # Downloads dependencies
.\build.ps1               # Compiles both halves
.\package.ps1             # Wraps installer
```


- If blocked, insert `powershell -ExecutionPolicy RemoteSigned -File ` at the top of command.
- `package.ps1` needs Inno Setup (`winget install JRSoftware.InnoSetup`).

For development, `install.ps1` and `uninstall.ps1` register `dist\` in place and skip the packaging step. `build.ps1 -SelfContained` bundles a private .NET runtime instead of requiring the shared one.

## Description

- **Output** — every conversion, resize, compression and rotation writes a new file beside the original. Sources are never modified or replaced. Name collisions get an `(2)` suffix like Explorer.
- **Deletion** — Remove Duplicate and Remove Empty move to the Recycle Bin, so every deletions can be undone.
- **Duplicates** — Matched on size, a 64 KB head hash, then full contents. Files are only ever called duplicates once their bytes match.
- **Menu implement** — Classic `IContextMenu` handler, so on Windows 11 it lives under "Show more options" rather than the short menu. 
- **Menu improvement** — To show Flect menu by only one click, run `reg add "HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" /f /ve & taskkill /f /im explorer.exe & start "" explorer.exe` on terminal.
- **Interrupted work** — Process that fails or is cancelled deletes what it was being written. Video compression stages through a hidden scratch directory beside the destination, so the output only appears once it is complete. Forcely killed process cannot clean up after itself, so leftovers older than an hour are swept on the next run in that folder.

## Video compression

Size-target compression works down a ladder, stopping at the first rung that gets under the target: repackage the streams, re-encode only the audio, then re-encode the picture — and after that, correct the bitrate from the size the first attempt actually produced and try once more.

Flect is built for easy usage, so it does not support detailed output settings. Consider using [Compressor web](https://github.com/Konayukiw/Compressor) instead if you want edit settings for compression.

### HEIC conversion

There's no free-to-use encoder for HEIC. ImageMagick's Windows build, libheif's own releases and ffmpeg all read it and none of them write it, because an HEVC encoder cannot ship under those licences. ImageMagick does not even fail when asked — it silently writes a PNG under a `.heic` name.

Windows does have an encoder, in the **HEVC Video Extensions**, and WPF can reach it through WIC. So `to HEIC` is routed there ([`ImageIo`](src/App/Core/ImageIo.cs)) and everything else stays in ImageMagick. On a machine without that codec installed, `to HEIC` fails with a message saying so. Reading HEIC works everywhere.


## Dependencies

| Requirements | Comes from | Installation |
| --- | --- | --- |
| Runtime for Flect | .NET 10 Desktop Runtime | Setup automatically checks |
| Images | Magick․NET | Setup automatically downloads |
| Video | ffmpeg / ffprobe | Setup automatically downloads |
| PDF | PDFium / PDFtoImage / PdfPig | Setup automatically downloads |
| Decompile .jar | cfr.jar | Setup automatically downloads |
| Decompile precondition  | JDK / JRE | Download by yourself |
| HEIC conversion | Windows HEVC Video Extensions | Download by yourself |

## Licenses

- **Magick․NET**  
  https://github.com/dlemstra/Magick.NET  
  Licensed under the Apache License 2.0  
  (Based on ImageMagick: https://imagemagick.org/)

- **FFmpeg** (ffmpeg / ffprobe)
  https://ffmpeg.org/  
  Licensed under the LGPLv2.1
  Source code: https://github.com/ffmpeg/ffmpeg

- **CFR** (cfr.jar)  
  https://www.benf.org/other/cfr  
  Licensed under the MIT License  
  Copyright (c) 2011- Lee Benfield