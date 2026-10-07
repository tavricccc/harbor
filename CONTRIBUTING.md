# 參與開發

Harbor 的程式碼維護在 `winui-native` 分支。下載引擎是固定版本的 `GopeedLab/gopeed` submodule；建置方式見 [README](README.md)。

1. 使用 Windows、PowerShell 7、.NET 10 SDK 與 Go 1.27 建置。
2. 遞迴取得 submodule，執行 `pwsh -File scripts/build.ps1 -Test`。
3. 將變更拆成可維護的服務、模型與介面元件；沿用原生控制項及 ThemeResource。
4. 行為變更需驗證真實流程；外觀變更至少附上實際 WinUI 截圖。不要用自製 HTML 畫面代替原生驗證。
5. 向 `tavricccc/harbor` 的 `winui-native` 分支送 PR，說明問題、修改後的行為及實際做過的檢查。

Core 更新需明確改變 submodule commit，檢查 API 與 browser integration 相容性。不要將 API Token、使用者 session、下載 Cookie、簽章私鑰或個人測試檔提交。

安裝版資料位於 `%LOCALAPPDATA%\GopeedNative`。測試只能移除自己建立的任務與檔案，請保護使用者既有下載。
