# Harbor

[繁體中文](README.zh-TW.md) · English

A native Windows download manager built with WinUI 3 and the official [Gopeed](https://github.com/GopeedLab/gopeed) download engine. Harbor maintains the Windows interface, tray, browser handoff and scheduling; download protocols and storage come from the Gopeed Go module.

## Download

[Harbor 0.10.1 for Windows x64](https://github.com/tavricccc/harbor/releases/tag/v0.10.1) — preview release.

- [Installer](https://github.com/tavricccc/harbor/releases/download/v0.10.1/Harbor-Setup-0.10.1-x64.exe)
- [Complete source](https://github.com/tavricccc/harbor/releases/download/v0.10.1/Harbor-Source-0.10.1.zip)
- [SHA-256 checksums](https://github.com/tavricccc/harbor/releases/download/v0.10.1/SHA256SUMS.txt)

Windows 10 1809 or later; Windows 11 recommended. The per-user installer includes the runtime and upgrades existing installations. Downloads and settings are preserved during upgrades.

## Features

- HTTP/HTTPS connections, pause and resume; BitTorrent, magnet and eD2k.
- Browser downloads through the official Gopeed extension.
- Batch links, search, filters, deferred downloads and one-time schedules.
- Inline download options and connection progress.
- English, Traditional Chinese and Simplified Chinese; Windows themes.

Startup at sign-in is enabled by default. Saving by category is off by default. Closing a window leaves downloads running; quit from the tray to stop the background engine.

## Documentation

- [Usage](docs/usage.md)
- [Development and upstream updates](docs/development.md)
- [Changes](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)

## Build

On Windows, install PowerShell 7, .NET 10 SDK, Go 1.27 and Inno Setup 6:

```powershell
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
```

Outputs are in `artifacts/`. Source is maintained on `winui-native`. Licensed under [GPL-3.0](LICENSE).
