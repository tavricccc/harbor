# Development

[繁體中文](zh-TW/development.md) · [Home](../README.md)

Use Windows, PowerShell 7, .NET 10 SDK, and Go 1.27. Work on `winui-native`.

## Ownership

| Path | Responsibility |
| --- | --- |
| `src/Harbor/` | WinUI views, models and Windows services |
| `src/HarborSetup/` | Per-user WinUI setup and uninstaller |
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

Publishing uses shared .NET and Windows App Runtime, without NativeAOT, trimming or ReadyToRun. JSON catalogs, preferences and statistics use generated serializers. Integration registration runs before Windows App Runtime initialization so installer/uninstaller commands do not need WinUI. The setup is itself a framework-dependent WinUI app: missing .NET shows the .NET host's download prompt and missing Windows App Runtime shows the SDK's, both before any file is changed. Its UI is built in code because a single-file app extracts compiled XAML away from the executable, where WinUI does not look for it.

For a local installer that should not replace a release artifact, use `pwsh -File scripts/package.ps1 -SkipBuild -OutputDirectory artifacts/shared-runtime`. The setup embeds the published app as a zip with `installed-files.txt`, removes files the new version no longer ships, and copies itself to `Uninstall.exe`; it omits PDB symbols. Upgrades from the Inno Setup installer (0.11 and earlier) keep its install location and remove its uninstaller and entry. `Harbor.exe --check-dotnet` checks the .NET host; `Harbor.exe --check-runtime` also checks the matching Windows App Runtime. Both exit without showing the application or changing integrations.

## Upstream updates

```powershell
# Latest published release, including previews
pwsh -File scripts/update-core.ps1 -Channel preview -Test
# Current development branch
pwsh -File scripts/update-core.ps1 -Channel main -Test
```

`main` includes unreleased changes. Builds use the version already locked in `go.mod`; only the update command changes it. Commit `go.mod`, `go.sum` and `core/upstream.json` together. The fork's GitHub behind count measures branch history, not the module version used by Harbor.

## Localization

Edit `localization/en-US.json`, `zh-TW.json` and `zh-CN.json` together. Keep keys and `{0}` format arguments aligned. Add languages to `languages.json`. C#, the setup and Go embed these files; the setup uses the `Installer.*` entries.

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

Click **Run activation checks** in the test window, or add `--auto` to run on an interactive desktop without clicking. The check speaks the official Native Messaging protocol, verifies foreground/topmost state and refusal of native Z-order demotion, selects/cancels an owned folder picker, then starts and cancels an isolated test task to verify topmost release. It does not change browser registration or use personal download data. Add `--background` on a noninteractive desktop to omit keyboard-foreground assertions; the remaining lifecycle checks still run. Actual browser-extension clicks require a separate interactive check. Refused foreground activation is recorded in the profile's `activation.log` without request URLs or headers.

The picker check compares actual Z-order against every confirmation, including multiple open requests. Shared-runtime failure checks use an isolated host variant so the machine's installed runtimes and firewall settings remain unchanged:

```powershell
dotnet publish src/Harbor/Harbor.csproj -c Release -r win-x64 -p:Platform=x64 -p:AppHostDotNetSearch=EnvironmentVariable -o artifacts/tests/missing-dotnet-app
pwsh -File tests/Harbor.ActivationChecks/RuntimeChecks.ps1 -ApplicationDirectory artifacts/app -MissingDotNetDirectory artifacts/tests/missing-dotnet-app
dotnet run --project tests/Harbor.ActivationChecks -c Release -- "$PWD/artifacts/app" --runtime-dialogs "$PWD/artifacts/tests/missing-dotnet-app"
```

The last command verifies the actual installation prompts and declines both downloads. A blocked foreground fixture is not a passing foreground test. Keep clean-machine installer/upgrade/uninstaller checks separate from build and package verification.
