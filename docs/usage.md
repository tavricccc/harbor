# Usage

[繁體中文](zh-TW/usage.md) · [Home](../README.md)

## Downloads

Choose **New download** or press Ctrl+N, paste a link and select a folder. One link per line creates a batch. Ctrl+V and drag-and-drop accept links and Torrent files.

The first action checks the file; **Start download** begins the transfer. Torrent files can be selected individually. The lower-left arrow expands options within the window; HTTP headers and advanced sections are collapsed initially. The expanded area scrolls when needed.

In a progress window, click the source URL to open it or right-click to copy it. The lower-left arrow shows connection progress and sampled speeds. **Cancel** removes the active task and keeps partial files. Closing the window leaves the task running.

## Queue and schedule

**Download later** saves a request without starting it. **Schedule** sets a one-time start. Open the deferred list from **More** to start, retry or change the time.

Schedules need the background engine. Missed schedules run the next time Harbor starts; Harbor does not wake a sleeping or powered-off computer.

## Settings

Save applies changes; Cancel discards them. **Interface** controls language, theme and startup. Reopen the windows after changing language. Category folders are available only when saving by category is enabled.

Use the tray's Quit command to stop Harbor completely. Uninstall removes application settings, task records and registrations; downloaded files are kept.

## Browser integration

Install the official Gopeed extension in your browser:

- [Chrome](https://chromewebstore.google.com/detail/gopeed/mijpgljlfcapndmchhjffkpckknofcnd)
- [Edge](https://microsoftedge.microsoft.com/addons/detail/dkajnckekendchdleoaenoophcobooce)
- [Firefox](https://addons.mozilla.org/firefox/addon/gopeed-extension)

Harbor registers its local host during installation. If downloads stop reaching Harbor, open **Settings → Network** and choose **Repair registration**. Then confirm the extension is enabled in the browser. The extension's [documentation](https://github.com/GopeedLab/browser-extension) covers its own settings.
