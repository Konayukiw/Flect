<div align="center">

# Flect

The all-in-one Windows Explorer extension. Convert, Compress, Resize, Rotate, Trim, Bulk Rename... Everything can be done in right clicking the file in Explorer.

https://github.com/user-attachments/assets/52703db0-1c86-40a6-8b76-5c1300fe9277

</div>

## Update version <VERSION>



<DESCRIPTION>



## Features

### Images

| | |
| --- | --- |
| **Resize** | Scale down by percentage, or to custom pixels |
| **Compress** | Squeeze down to a target file size |
| **Rotate** | Any angle - corners stay transparent, or turn white if the format has no alpha |
| **Convert** | Between PNG, JPG, WEBP, HEIC, ICO, GIF and SVG |
| **OCR** | Read the text out of an image, to a file, the clipboard, or just on screen |
| **Remove Metadata** | Strip EXIF, GPS coordinates, comments and camera information |
| **Remove Background** | Make one colour transparent |

> OCR requires Windows language pack with OCR enabled. If you haven't downloaded the language you need, read [FAQ](#faq) for language pack setup.

### Videos & audios

| | |
| --- | --- |
| **Resize** | Scale down by percentage or to custom pixels |
| **Trim** | Scrub to the part you want of one video or audio file and keep just that - instantly, no re-encoding |
| **Thumbnail** | Export any single frame as a PNG or JPG image |
| **Subtitles** | Transcribe the spoken words into an SRT, VTT or TXT file |
| **Compress** | Hit a target size - including a one-click Discord preset |
| **Rotate** | Any angle, with a chosen fill colour |
| **Convert** | Between MP4, MOV, MKV, M4A, AVI, WEBM, FLV,  animated GIF |
| **Extract Audio** | Extract soundtrack out to MP3, M4A, WAV, AAC, OGG and more |

### Folders

| | |
| --- | --- |
| **Remove Duplicate** | Find identical files and remove them |
| **Remove Empty** | Clear out empty folders |
| **Tree** | Print the folder structure |
| **Rename** | Batch rename with a custom prefix, suffix or numbering |
| **Analyze** | Show total size, largest files, breakdown by file type, duplicates and empty folders |
| **Compress** | Pack a folder into a ZIP, 7z, TAR or tar.gz |

Duplicates are matched on size, hash and per-byte comparison.

### Archives

| | |
| --- | --- |
| **Extract** | Unpack ZIP, 7z, TAR, tar.gz and RAR archives into a new folder |

RAR is read-only: Flect can extract `.rar` files but cannot create them. This is technical capacity and not caused by my laziness.

### Text files

| | |
| --- | --- |
| **PDF** | Preview, merge several into one, or export to PNG, JPG or TXT |
| **TEXT** | Convert between data formats, change the encoding, or switch line endings |
| **JSON** | Pretty print, sort keys with rules |
| **JAR** | Decompile into a folder of readable Java source |

## Conversion style

| From | To |
| --- | --- |
| **PNG, JPG, WEBP, HEIC, ICO, GIF, SVG** | Any other format in this row |
| **MP4, MOV, MKV, M4A, AVI, WEBM, FLV** | Any other format in this row + GIF |
| **MP3, WAV, AIF, AIFF, AAC, OGG, WMA** | Any other format in this row + M4A |
| Video → **Extract Audio** | MP3, M4A, WAV, AIF, AIFF, AAC, OGG, WMA |
| **PDF** | PNG, JPG, TXT |
| **Any text file** | TXT |
| **CSV** | JSON, XML, HTML, MD |
| **JSON** | CSV, XML |
| **XML** | JSON |
| **INI, CFG, CONF** | JSON, XML |
| **JAR** | A folder of decompiled `.java` sources |
| **Folder** | ZIP, 7z, TAR, tar.gz archive of the folder |
| Text encodings | UTF-8, UTF-8 (BOM), UTF-16, Shift_JIS |
| Line endings | CRLF (Windows), LF (Unix) |

## Usage

### Installer

1. Download the latest `Flect-Setup.exe` from [Releases](https://github.com/Konayukiw/Flect/releases/latest).
2. Run Setup and complete installation.

### PowerShell

```powershell
irm https://flect.konayuki.xyz/install.ps1 | iex
```

<details><summary>Caution</summary>
PowerShell scripts provided in the format `irm <URL> | iex` are commands that directly execute files hosted on the web. Although Flect does not engage in privacy-violating activities such as data interception, make sure the script is actually safe when executing any PowerShell scripts, as this same method could potentially be used to download malware.
</details>
　

The script downloads the latest Setup from [Releases](https://github.com/Konayukiw/Flect/releases/latest), checks its SHA256 against the one GitHub publishes, and installs Flect. If the .NET 10 Desktop Runtime is missing, it asks before installing it, and Windows then asks for administrator permission.

After installing, right-click any file or folder and choose **Show more options** to find Flect.

> **Why "Show more options"?**  - Windows 11 gives its short right-click menu. You can bring it up to the top level. See [FAQ](https://github.com/Konayukiw/Flect/blob/main/README.md#faq) below.

To change detailed settings, open **Flect** from the Start menu.
