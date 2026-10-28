# Contributing to Flect

Thank you for your interest in contributing to Flect.

Please read this document before contributing. It helps smooth reviews and keeps maintainability.

---

## General

- **Respect and Kindness**: Please communicate with respect and patience in issues and pull requests.
- **No Advertisement**: Do not post any issues, pull requests or comments including referral to any third party services for your self purposes on this repository.
- **Safety First**: Original user files must never be lost, corrupted, or altered in unexpected ways.
- **No Vibe Contributing**: Getting assisted by AI for contributing is permitted but automated, unverified or any vibe coded pull requests will be rejected. You must test, verify in your branch, and understand any changes before you submit.

---

## How to Contribute

### Issues

- Search existing issues first to confirm the problem has not already been reported.
- Fulfill the required information in issue template.

### Ideas

- **Open an issue first before writing code.**
- Flect is designed around a lightweight, focused context menu.
- Fulfill the required information in issue template.

### Trivial Fixes

- Small fixes such as typo fixes do not require an issue upfront. You can submit a pull request directly.

---

## Architecture

### Directory structure

```
Flect/
├── src/
│   ├── Shell/         # Native C++ Explorer context menu handler (IContextMenu)
│   │   └── Modern/    # Windows 11 sparse package modern menu (IExplorerCommand)
│   ├── App/
│   │   ├── Assets/    # Application icons and branding graphics
│   │   ├── Gui/       # WPF windows, dialogs, pages, and custom controls
│   │   ├── Main/      # Core logics
│   │   └── Tasks/     # Concrete task implementations
│   └── Web/           # Project homepage and installation script web assets
├── dependencies/      # Third-party native binaries downloaded by scripts/fetch.ps1
├── deps/              # Python scripts for AI features
├── evals/
│   ├── Evals/         # .NET evaluation and integration test suite
│   ├── i18n/          # Translation key coverage checker
│   └── installer/     # Automated setup and installer verification scripts
├── scripts/           # PowerShell build, packaging, and setup automation
│   ├── build.ps1      # Compiles Shell extension and App worker
│   ├── fetch.ps1      # Fetches third-party binaries (FFmpeg, 7-Zip, CFR)
│   ├── package.ps1    # Packages the Inno Setup installer
│   └── modern/        # Build and test helpers for Windows 11 modern context menu
└── setup/             # Inno Setup script for installer and manifests
```

### Naming

- **C# (.NET)**: Follow standard .NET naming conventions. Use PascalCase for classes, records, methods, and properties. Use camelCase for local variables and parameters.
- **C++ (Shell)**: Use PascalCase for classes, structs, and methods. Match existing Win32 COM naming patterns in `src/Shell`.
- **Scripts and Folders**: Use lowercase for scripts and folder paths unless matching .NET namespace conventions.
- **Directory**: Maintain the current directory structure directly under `Flect/`, unless specific large modifications are required.

---

## Development

### Requirements

- **OS**: Windows 10 or 11 (x64)
- **.NET SDK**: .NET 10.0 SDK
- **C++ Build Tools**: Visual Studio Build Tools (2022 or 2026) with "Desktop development with C++" and Windows SDK (10.0.19041.0 or newer)
- **Node.js**: Node.js 20 or newer (for translation key checks)
- **Optional**: JDK 21 (for decompilation evaluation tests), Inno Setup 6.7+ (for installer packaging)

### Quick Start

```powershell
# 1. Download third-party binaries (FFmpeg, 7-Zip, CFR)
.\scripts\fetch.ps1

# 2. Build native Shell extension and .NET App
.\scripts\build.ps1
```

### Verification

Before opening a pull request, run the relevant checks:

```powershell
# Translation parity check (EN, JA, ZH)
node evals/i18n/check.mjs

# Core test suite
dotnet test evals/Evals/Evals.csproj -c Release --filter-not-trait "Category=AI"

# Modern context menu test (if modifying src/Shell/Modern)
.\scripts\modern\test.ps1
```

---

## Guidelines

- **Non-Destructive**: Never overwrite user files. Append collision suffixes like `(2)` and move deletions to the Recycle Bin. Clean up temporary files on failure or cancellation.
- **Shell Responsiveness**: `src/Shell` runs directly inside `explorer.exe`. Never block the Explorer UI thread. Delegate all heavy work to `src/App`.
- **Localization**: When adding or updating strings, update `Strings.En.cs`, `Strings.Ja.cs`, `Strings.Zh.cs`, and `src/Shell/Labels.h`. Run `check.mjs` to ensure key parity.
- **Catalog Synchronization**: Keep menu definitions synchronized across `src/Shell`, `src/Shell/Modern`, `MenuCatalog.cs`, and `General.xaml`.
- **Dependencies**: Open an issue before adding any external library or NuGet package. Keep installer footprint minimal and document licenses in `NOTICES.md`.
- **Code Style**: Write clean, self-explanatory code without redundant docstrings or comments.
- **Commit Format**: Use the format `<Verb> <description>` (Ex: `Add feature`, `Fix #123`, `Improve #123`, `Remove feature`).

---

## Pull Request Checklist

1. Fork the repository and create a branch from `main`.
2. Keep each pull request focused on a single topic.
3. Verify that build and tests pass locally.
4. Provide a clear description, link relevant issues (`Closes #123`), and include screenshots for UI or Explorer changes.

---

## License

Contributions are licensed under the [Apache License 2.0](/LICENSE). Existing copyright notices and attribution to @Konayukiw must remain intact. Read [README](/README.md#license) for more.
