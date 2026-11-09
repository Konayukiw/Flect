# Security Policy

## Supported versions

Security fixes ship in the latest release. Older releases are not patched on their own. Install the current build from [Releases](https://github.com/Konayukiw/Flect/releases/latest).

## Reporting a vulnerability

Report vulnerabilities through [Private Vulnerability Report](https://github.com/Konayukiw/Flect/security/advisories/new).

Do not open a public issue or pull request for a security report. The issue form is for ordinary bugs.

Describe the affected part, how to reproduce it, and the impact if you know it. A suggested fix is welcome and not required.

There is no fixed response time. Reports are read by the maintainer and answered when there is something to say.

The entire application, original source code, the installer, and the install script at <https://flect.konayuki.xyz/install.ps1> are in scope.

The Explorer preview handles untrusted files by design, so reports about it are especially welcome. That includes a previewed file reaching the network while network access is off, running a script in a restricted preview, or escaping the preview's sandboxed frame.

Flaws in Chromium, the WebView2 Runtime or FFmpeg themselves belong with those projects. A way to reach them from the preview that Flect should have blocked is in scope here.
