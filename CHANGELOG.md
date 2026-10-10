# Changelog

[繁體中文](CHANGELOG.zh-TW.md)

## 0.10.2 — 2026-10-11

- Keep download confirmations topmost through native frame changes, resizing and activation changes; release topmost when downloading starts or the confirmation closes.
- Separate topmost placement from foreground activation so a refused activation does not block placement. Record refused activation in `activation.log` without download URLs or headers.
- Replace legacy Browse/Torrent pickers with owned Windows App SDK desktop pickers, including settings, category folders and extension folders. Start Browse at the current destination, handle cancellation/errors, and prevent repeated clicks while the picker is open.
- Extend native checks to cover foreground activation, forced Z-order demotion, folder selection/cancellation, starting a download and closing confirmations. The previous 0.10.1 build fails the demotion regression; this build passes.

## 0.10.1 — 2026-10-10

- Pass foreground permission from the browser host through the background engine to the download confirmation, including activation of an existing Harbor instance.
- Keep isolated activation checks from changing browser registration or startup preferences.

- Move shared language catalogs into `localization/`, including installer text, and read language options from one manifest.
- Separate download form initialization, request handling and pickers; separate Windows browser, startup and file-type integration.
- Split engine startup, authenticated control endpoints and session persistence.
- Use typed statistics shared by progress and details views; update the official Gopeed module to `main` at `ba84a04a434b`.
- Replace duplicated documentation with short English and Traditional Chinese usage and development guides.

## 0.10.0 — 2026-10-09

- Inline, scrollable download settings and connection progress; keep the window centered during expansion.
- Clickable source URLs with a copy command; collapsed HTTP headers and shorter settings copy.
- Startup enabled by default; category folders require opting in.
- Remove obsolete program files on upgrade and migrate legacy data; clean application residue on uninstall while keeping downloads.

## 0.9.0 — 2026-10-08

- Add English and Simplified Chinese alongside Traditional Chinese, with saved language selection and a multilingual installer.

## 0.8.0 — 2026-10-08

- Consume the official Gopeed Go module; reorganize native task controls and settings, and reduce unnecessary refresh work.

Earlier entries are preserved in the [Traditional Chinese history](CHANGELOG.zh-TW.md).
