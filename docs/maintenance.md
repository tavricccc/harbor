# 開發與維護

## 程式碼位置

| 位置 | 職責 |
| --- | --- |
| `core/` | 背景程序、Windows 整合、瀏覽器確認與持久排程；下載引擎使用官方 Gopeed module |
| `src/Harbor/Services/CoreClient.cs` | 核心啟動與認證、串流讀取 API JSON、取消請求及核心版本 |
| `src/Harbor/Models/DownloadItem*.cs` | 任務資料、快照變更分類與獨立的 WinUI 列綁定 |
| `src/Harbor/ViewModels/DownloadsViewModel*.cs` | 刷新、篩選排序與操作分開維護，保留清單項目和選取 |
| `src/Harbor/MainPage.Refresh.cs` | 搜尋延遲、視窗可見性與輪詢節奏 |
| `src/Harbor/Views/` | 共用確認表單、可重開進度窗、設定與詳情 |
| `core/localization/*.json` | 前端與背景核心共用的英文、繁體中文及簡體中文語系資源 |
| `src/Harbor/Localization/` | 語系選擇、文化格式與 XAML 字串載入 |
| `scripts/` | 核心更新、建置、安裝包與完整原始碼包 |

工具列的選取操作與狀態啟用參考 [IDM 官方主視窗說明](https://www.internetdownloadmanager.com/support/main.html)，版面比例與控制項沿用 Harbor 既有的 WinUI 介面。

## 本機檢查

```powershell
pwsh -File scripts/build.ps1 -Test
dotnet run --project tests/Harbor.ProtocolChecks/ProtocolCheck.csproj -c Release -- --benchmark
```

`-Test` 檢查 Go module 校驗碼、核心整合測試、瀏覽器 host、前端協定／任務模型，再 publish WinUI x64。UI 使用原生控制項和系統主題資源；外觀驗收需實際 WinUI 畫面，不能用 HTML 替代。

2026-10-08 在此 Windows x64、.NET 10 環境測得下列模型微基準。使用 1,000 筆合成任務、30 次相同快照，暖身 3 次；不含網路、XAML 繪製或整個程序記憶體：

| 更新方法 | 30 次耗時 | 同步執行緒配置量 |
| --- | ---: | ---: |
| 舊版兩份 JSON 轉字串比較 | 260.3 ms | 56.25 MiB |
| 新版 `DownloadItem.Update` 結構比較 | 46.0 ms | 0.69 MiB |

下載進度變動時只通知傳輸數據。狀態、名稱、來源、檔案大小或解壓狀態改變才重建篩選／排序；選擇按進度排序時仍會隨進度調整。搜尋等待 180 ms，活躍清單每秒更新，閒置每 5 秒更新。主清單隱藏或最小化時停止輪詢；進度窗也採同樣可見性規則，完成後停止更新。

本次已通過核心／協定／模型檢查和 WinUI publish。另在獨立資料目錄啟動已發布到本機輸出目錄的核心，確認版本為 `2.0.0-beta.3`，任務／排程 API 與正常關閉通過。沒有啟動原生視窗做互動驗收；字級、裁切、高 DPI、深淺色、高對比、視窗恢復與瀏覽器喚醒仍需桌面檢查。上述微基準不能推算整個程式的 RAM 節省。

## 發行

產品版本只改 `src/Harbor/Harbor.csproj` 的 `Version`，安裝包與原始碼包會自動讀取。核心 release 則由更新腳本取得，流程見 [核心維護](core-updates.md)。

```powershell
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
pwsh -File scripts/source.ps1
```

輸出位於 `artifacts/`。`source.ps1` 以已提交的 Harbor 原始碼為基礎，附上與建置相同的 Gopeed module 來源；請先提交產品變更再製作原始碼包。保留 `CHANGELOG.md` 的舊版本紀錄，README 的下載連結只在對應資產已發布後更新。

安裝與解除安裝的整合測試會修改使用者註冊，請在取得同意後進行。下載測試使用獨立 `HARBOR_DATA_DIRECTORY`，避開日常使用的連接埠及下載資料。

## 語系維護

三套語系為 `en-US`、`zh-TW`、`zh-CN`，使用相同的語意鍵，例如 `Downloads.Add`。新增字串時一併補齊三份 JSON；格式參數使用 `{0}`、`{1}`，讓翻譯能調整順序。C# 使用 `Strings.Get`／`Strings.Format`，XAML 使用 `{loc:Localize Key=Downloads.Add}`。字串直接內嵌到前端與核心，不需在安裝目錄維護額外的語系檔。

```powershell
pwsh -File scripts/check-localization.ps1
dotnet run --project tests/Harbor.ProtocolChecks/ProtocolCheck.csproj -c Release
```

前者檢查程式碼中的資源鍵，後者檢查三套資源完整性、格式參數、Windows 語言選擇及錯誤訊息。`build.ps1 -Test` 也會執行。偏好檔 `preferences.json` 的 `Language` 為空字串時跟隨 Windows；其他值是上述語系代碼。切換在前端下次啟動時套用，透過已驗證 Token 的核心 `/language` 控制端點同步托盤與通知，保留原有下載程序。

0.9.0 的原生介面尚未進行視覺與互動檢查；建置及資源檢查不代表已確認英文裁切、高 DPI 或系統選擇器的顯示。
