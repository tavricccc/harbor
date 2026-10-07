# 參與開發

Harbor 的程式碼維護在 `winui-native` 分支。下載引擎使用官方 `GopeedLab/gopeed` Go module；建置方式見 [README](README.md)。

1. 使用 Windows、PowerShell 7、.NET 10 SDK 與 Go 1.27 建置。
2. 執行 `pwsh -File scripts/build.ps1 -Test`，Go 會取得核心相依套件。
3. 將變更拆成可維護的服務、模型與介面元件；沿用原生控制項及 ThemeResource。
4. 行為變更需驗證真實流程；外觀變更至少附上實際 WinUI 截圖。不要用自製 HTML 畫面代替原生驗證。
5. 向 `tavricccc/harbor` 的 `winui-native` 分支送 PR，說明問題、修改後的行為及實際做過的檢查。

核心更新使用 `pwsh -File scripts/update-core.ps1 -Channel preview -Test`，一起提交 `core/go.mod`、`core/go.sum` 與 `core/upstream.json`。HTTP／BT／eD2k、儲存、解壓縮與擴充功能交由上游實作；Harbor 的排程、Windows 系統匣與瀏覽器確認保留在 `core/`，不要直接修改 module cache。流程與相容性檢查見 [核心維護](docs/core-updates.md)。

不要將 API Token、使用者 session、下載 Cookie、簽章私鑰或個人測試檔提交。

Windows 核心使用 anacrolix 的 `classic` 檔案 I/O，避免 mmap 長期鎖住未選取的 Torrent 檔案。建置腳本會設定測試環境；直接執行 Go 測試時，請先設定 `$env:TORRENT_STORAGE_DEFAULT_FILE_IO = 'classic'`。

安裝版資料位於 `%LOCALAPPDATA%\Harbor`。測試只能移除自己建立的任務與檔案，請保護使用者既有下載。

實機測試或錄製示範時，可用 `HARBOR_DATA_DIRECTORY` 指定獨立資料目錄。不同目錄使用獨立介面實例；請在該目錄的 `preferences.json` 設定不同的 `ApiPort`，避免與日常使用的核心衝突。
