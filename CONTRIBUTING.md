# Contributing

[Development](docs/development.md) · [開發與維護](docs/zh-TW/development.md)

Submit pull requests against `winui-native`. Keep changes within Harbor's UI and integration layer; update the official Gopeed module for engine changes.

Split code by responsibility and keep translations in `localization/`. Run `pwsh -File scripts/build.ps1 -Test`, then describe the changed behavior and the checks performed. Include native screenshots for visual changes.

Do not commit tokens, cookies, user data or signing keys.
