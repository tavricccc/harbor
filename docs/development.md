# Development

[繁體中文](zh-TW/development.md) · [Home](../README.md)

Use Windows, PowerShell 7, .NET 10 SDK, Go 1.27 and Inno Setup 6. Work on `winui-native`.

## Ownership

| Path | Responsibility |
| --- | --- |
| `src/Harbor/` | WinUI views, models and Windows services |
| `core/` | Engine lifecycle, tray, browser handoff and deferred queue |
| `localization/` | One JSON catalog per language, shared by C# and Go |
| `go.mod`, `go.sum`, `core/upstream.json` | Locked upstream dependency and provenance |
| `scripts/` | Build, upstream update and packaging |

Gopeed owns download protocols, task storage, extraction and extensions. Update its official module rather than maintaining a patched engine or merging its Flutter application into Harbor.

## Build and package

```powershell
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
pwsh -File scripts/source.ps1
```

`-Test` runs existing Go, protocol, model and localization checks before publishing the WinUI app. Commit source changes before `source.ps1`; the archive includes the locked Gopeed source and license. Output goes to `artifacts/`.

Product versions are in `src/Harbor/Harbor.csproj` and `Package.appxmanifest`. Update both and the changelogs together. Installer and source filenames read the project version.

## Upstream updates

```powershell
# Latest published release, including previews
pwsh -File scripts/update-core.ps1 -Channel preview -Test
# Current development branch
pwsh -File scripts/update-core.ps1 -Channel main -Test
```

`main` includes unreleased changes. Builds use the version already locked in `go.mod`; only the update command changes it. Commit `go.mod`, `go.sum` and `core/upstream.json` together. The fork's GitHub behind count measures branch history, not the module version used by Harbor.

## Localization

Edit `localization/en-US.json`, `zh-TW.json` and `zh-CN.json` together. Keep keys and `{0}` format arguments aligned. Add languages and their Inno Setup mapping to `languages.json`. C# and Go embed these files; packaging converts the `Installer.*` entries into Inno Setup messages. Edit the JSON sources, not the generated file in `artifacts/`.

Use `Strings.Get` / `Strings.Format` in C#, `{loc:Localize Key=...}` in XAML and `localization.Text` in Go.

```powershell
pwsh -File scripts/check-localization.ps1
```

## Local data

Application data is stored in `%LOCALAPPDATA%\Harbor`. Use `HARBOR_DATA_DIRECTORY` and a separate `ApiPort` in `preferences.json` for isolated manual runs. Keep tokens, cookies, user data and signing keys out of Git. Validate native UI changes with actual WinUI captures; build success alone does not establish visual correctness.

For browser activation changes, run this local integration check after building:

```powershell
dotnet run --project tests/Harbor.ActivationChecks -c Release -- "$PWD/artifacts/app"
```

Click **Run activation checks** in the test window. It uses the official Native Messaging protocol to check a new confirmation, redirection to an existing instance and a forwarded task request, then closes its isolated processes. It checks visibility, topmost state and keyboard foreground without starting downloads or changing browser registration. Add `--background` on a noninteractive desktop to check visibility and topmost state only; that mode does not verify keyboard foreground. Actual browser-extension clicks require a separate interactive check.
