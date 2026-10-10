# 開發與維護

[English](../development.md) · [首頁](../../README.zh-TW.md)

使用 Windows、PowerShell 7、.NET 10 SDK、Go 1.27 與 Inno Setup 6，在 `winui-native` 分支工作。

## 維護範圍

| 位置 | 職責 |
| --- | --- |
| `src/Harbor/` | WinUI 介面、模型與 Windows 服務 |
| `core/` | 核心程序管理、系統匣、瀏覽器轉交與待下載佇列 |
| `localization/` | 每種語言一份 JSON，C# 與 Go 共用 |
| `go.mod`、`go.sum`、`core/upstream.json` | 上游依賴版本與來源紀錄 |
| `scripts/` | 建置、上游更新與封裝 |

下載協定、任務儲存、解壓縮與擴充功能由 Gopeed 維護。更新官方 module 即可，避免維護修改過的引擎，或將上游 Flutter 應用程式合併到 Harbor。

## 建置與封裝

```powershell
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
pwsh -File scripts/source.ps1
```

`-Test` 在 WinUI publish 前執行既有的 Go、協定、模型與語系檢查。製作原始碼包前先提交變更；原始碼包會附上建置使用的 Gopeed 來源與授權。輸出位於 `artifacts/`。

產品版本在 `src/Harbor/Harbor.csproj` 與 `Package.appxmanifest`，更版時一起修改並更新紀錄。安裝器及原始碼包名稱從專案版本取得。

## 更新上游

```powershell
# 最新發布版，包含預覽版
pwsh -File scripts/update-core.ps1 -Channel preview -Test
# 目前的開發分支
pwsh -File scripts/update-core.ps1 -Channel main -Test
```

`main` 包含尚未發布的修改。一般建置只用 `go.mod` 已記錄的版本；更新後一起提交 `go.mod`、`go.sum` 與 `core/upstream.json`。GitHub fork 的落後數字比較分支歷史，不代表 Harbor 使用的 module 版本。

## 語系

同時修改 `localization/en-US.json`、`zh-TW.json` 與 `zh-CN.json`，維持相同的鍵與 `{0}` 格式參數。新增語言時在 `languages.json` 加入語言與 Inno Setup 對應。C# 與 Go 直接嵌入共用檔案；封裝會將 `Installer.*` 轉為安裝器文案。只修改 JSON，不修改 `artifacts/` 裡的產生檔案。

C# 使用 `Strings.Get`／`Strings.Format`，XAML 使用 `{loc:Localize Key=...}`，Go 使用 `localization.Text`。

```powershell
pwsh -File scripts/check-localization.ps1
```

## 本機資料

程式資料位於 `%LOCALAPPDATA%\Harbor`。手動測試可用 `HARBOR_DATA_DIRECTORY` 指定獨立目錄，並在 `preferences.json` 設定不同的 `ApiPort`。Token、Cookie、使用者資料與簽章私鑰不得提交到 Git。介面變更以真正的 WinUI 畫面檢查，建置成功不等於外觀已驗收。

修改瀏覽器啟動流程後，先建置，再執行本機整合測試：

```powershell
dotnet run --project tests/Harbor.ActivationChecks -c Release -- "$PWD/artifacts/app"
```

在測試視窗點「Run activation checks」，或加上 `--auto` 在互動桌面自動執行。測試透過官方 Native Messaging 格式驗證前景、持續置頂與原生降層攔截，實際選取／取消具有擁有者的資料夾 picker，再建立並取消隔離的測試任務，確認開始下載後解除置頂。不變更瀏覽器註冊或使用個人下載資料。非互動桌面可加上 `--background`，略過鍵盤焦點斷言，仍執行其他生命週期檢查。真正的瀏覽器擴充套件點擊須另做互動驗收。前景啟用遭拒會記錄在該資料目錄的 `activation.log`，不含下載網址或標頭。
