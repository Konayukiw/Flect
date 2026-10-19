# Contributing to Flect

Thank you for your interest in contributing to Flect. 

This guide outlines our development workflow, invariant architectures, and issues or pull request expectations. Reviewing these sections before submitting code helps ensure smooth reviews and keeps maintenance overhead manageable.

---

## Sections

1. [General](#general)
2. [How to Contribute](#how-to-contribute)
3. [Architecture](#architecture)
4. [Development](#development)
5. [Building and Testing](#building-and-testing)
6. [Core Invariants and Guidelines](#core-invariants-and-guidelines)
7. [Submitting a Pull Request](#submitting-a-pull-request)
8. [License](#license)

---

## General

- **Respect and Kindness**: Please communicate with respect and patience in issues and pull requests.
- **No Advertisement**: Do not post any issues, pull requests or comments including referral to any third party serivces for your self purposes on this repository.
- **Safety First**: Original user files must never be lost, corrupted, or altered in unexpected ways.
- **No Vibe Contributing**: Getting assisted by AI for contributing is permitted. However automated, unverified or any vibe coded pull requests will be rejected. You must personally test, verify in your branch, and understand any code in the commit you submit.

---

## How to Contribute

### Issues

- Search existing issues first to confirm the problem has not already been reported.
- If no matching issue exists, open a new issue detailing:
  - Your environment (Windows Edition, version, build)
  - Flect version
  - Exact steps to reproduce the behavior
  - File types, resolutions, or specific inputs involved
  - Expected and actual behavior
  - If exists, error messages or screenshots

### Ideas

- **Open an issue first before writing code.**
- Flect is designed around a lightweight, focused context menu. Adding menu items or complex workflows requires discussion to ensure they fit the project scope and user experience philosophy.
- Explain the use case, why existing tools are insufficient, and how the feature should appear in Explorer or the settings interface.

### Trivial Fixes

- Small fixes such as typos in documentation, minor translation adjustments, or straightforward regression fixes do not require an issue upfront. You can submit a pull request directly.

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

### Naming Conventions

- **C# (.NET)**: Follow standard .NET naming conventions. Use PascalCase for classes, records, methods, and properties. Use camelCase for local variables and parameters. Prefix private fields with an underscore (`_camelCase`). Keep files named after the primary class or component they contain.
- **C++ (Shell)**: Use PascalCase for classes, structs, and methods. Match existing Win32 COM naming patterns in `src/Shell`.
- **Scripts and Folders**: Use lowercase for scripts and folder paths unless matching .NET namespace conventions.
- **Directory**: Maintain the current directory structure directly under `Flect/`, unless specific large modifications are required.

---

## Development

To build and test Flect locally, you need the following tools:

- **OS**: Windows 10 or 11 (x64)
- **.NET SDK**: .NET 10.0 SDK
- **C++ Build Tools**: Visual Studio 2022 or 2026 Build Tools with the "Desktop development with C++" workload
- **Windows SDK**: Windows 10 SDK (10.0.19041.0 or newer)
- **Node.js**: Node.js 20 or newer (For translation check `evals/i18n/check.mjs`)
- **Java**: JDK 21 (For running JAR decompilation evaluations in `evals/Evals`)
- **Inno Setup**: Inno Setup 6.7 or newer (For packaging installers with `scripts/package.ps1`)

---

## Building and Testing

Open PowerShell in the repository root directory and execute the following steps:

### 1. Fetch Third-Party Dependencies

Download FFmpeg, 7-Zip, CFR, and other binaries:

```powershell
.\scripts\fetch.ps1
```

### 2. Build the Project

Compile both the native shell extensions and the .NET worker application:

```powershell
.\scripts\build.ps1
```

Outputs will be in the `dist/` directory.

### 3. Run Tests and Validations

Before submitting a pull request, run the following validation suites:

- **Translation Consistency Check**:
  Ensures all localization keys match across English, 日本語, and 简体中文.

  ```powershell
  node evals/i18n/check.mjs
  ```

- **Feature and Regression Tests**:
  Runs automated evaluation tests excluding GPU / AI-dependent tests:

  ```powershell
  dotnet test evals/Evals/Evals.csproj -c Release --filter-not-trait "Category=AI"
  ```

- **Windows 11 Modern Menu Tests** (If modifying `src/Shell/Modern`):

  ```powershell
  .\scripts\modern\test.ps1
  ```

---

## Core Invariants and Guidelines

Every contribution must adhere to the following principles:

### 1. Non-Destructive File Operations

- Never overwrite or mutate original files. All output files must be written alongside the original using collision suffixes such as `(2)`, `(3)`, identical to Windows Explorer behavior.
- In-place deletions, such as duplicate removal or empty folder cleanup, must send items to the Windows Recycle Bin by default. Permanent deletion must be an explicit user opt-in.
- If a processing task fails or is cancelled by the user, clean up any partially written temp files or output files.

### 2. Shell Extension Responsiveness

- Code running inside `src/Shell` executes directly in the `explorer.exe` process address space.
- Never block Explorer's synchronous UI thread. Heavy file inspection, hashing, or conversions must never run inside shell extension query methods. Defer inspection or delegate work to `src/App` worker processes.

### 3. Internationalization

- Flect supports English, 日本語, and 简体中文.
- If you add or modify a user-facing string, update all three localization files:
  - `src/App/Main/I18n/Strings.En.cs`
  - `src/App/Main/I18n/Strings.Ja.cs`
  - `src/App/Main/I18n/Strings.Zh.cs`
  - Also update shell menu labels in `src/Shell/Labels.h` or modern menu metadata where applicable.
- Always run `node evals/i18n/check.mjs` to verify key parity. If you are not fluent in one of the languages, provide reasonable placeholder translations and mention it in your pull request description for review.

### 4. Synchronized Menu Catalog

- When adding or changing menu entries, ensure consistency across:
  - Classic Shell context menu (`src/Shell`)
  - Modern Windows 11 context menu (`src/Shell/Modern`)
  - Worker process catalog (`src/App/Main/Process/MenuCatalog.cs`)
  - Settings page toggle lists (`src/App/Gui/Pages/General.xaml`)

### 5. Third-Party Dependencies and Licenses

- Do not add new NuGet packages, external binaries, or libraries without prior discussion in an issue.
- Every external dependency must comply with open-source licensing constraints, and new components must be documented in `NOTICES.md`.
- Keep installer bundle size in mind. Avoid introducing heavy libraries when built-in runtime capabilities or lightweight algorithms suffice.

### 6. Clean Code Style

Flect is a personal development software by @Konayukiw, so the subjectivity works. Pull requests including "messy" code styles may be refused.

- Write clean, self-explanatory code. Avoid redundant comments or verbose docstrings that merely restate method signatures.
- Preserve existing formatting conventions, file structures, and error handling patterns throughout the repository.

### 7. Commit Messages

Commit message should be written in following format:

Add feature123
Remove feature123
Fix #123
Improve #123
Merge pull request #123 from owner/branch

---

## Submitting a Pull Request

1. **Fork the Repository**: Create a personal fork and create a branch based on latest `main`.
2. **One PR for One Topic**: Keep pull requests focused on a single feature, bug fix, or refactor. Avoid mixing multiple unrelated changes into one pull request.
3. **Run CI Checks Locally**: Ensure that `scripts/build.ps1`, `node evals/i18n/check.mjs`, and `dotnet test` all succeed on your machine.
4. **Write a Clear Description**:
   - Explain what problem this pull request solves and why the chosen implementation was used.
   - Reference related issues using `Closes #123` or `Fixes #123`.
   - Include screenshots or screen recordings if your changes affect the GUI or Explorer menus.
5. **Respond to Review Feedback**: Maintainers may ask for adjustments or clarification. We appreciate your cooperation.

---

## License

By contributing to Flect, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).

- Existing copyright notices, license notices, and attribution notices cannot be removed or altered without authorization.
- Contributions must not misrepresent the original authorship of existing code or other project materials.
- Contributors must not remove or obscure existing attribution to the original author, @Konayukiw.
- Contributors retain copyright in their original contributions, subject to the applicable license terms.

All contributions must comply with the Apache License 2.0.
