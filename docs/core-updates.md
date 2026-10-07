# 跟進 Gopeed 核心

Harbor 直接引用官方 Gopeed Go module。下載協定、任務儲存、代理、解壓縮及擴充功能都使用上游實作；Windows 系統匣、確認視窗轉交與稍後下載放在 Harbor 的 `core/`。

## 更新

在專案根目錄使用 PowerShell 7：

```powershell
# 最新發布版，包含 Gopeed 2.0 預覽版；更新後執行本機檢查與 WinUI publish。
pwsh -File scripts/update-core.ps1 -Channel preview -Test

# 只跟進正式版。
pwsh -File scripts/update-core.ps1 -Channel stable -Test

# 指定已發布的官方版本。
pwsh -File scripts/update-core.ps1 -Version v2.0.0-beta.3 -Test
```

更新腳本查詢 [官方 GitHub Releases](https://github.com/GopeedLab/gopeed/releases)，解析 release tag 後由 Go 更新相依套件。`preview` 選最新發布的非草稿版本；`stable` 排除預覽版。切換到較舊的正式版屬於降版，請先在獨立資料目錄驗證，避免讓舊引擎讀取新格式的日常資料。

Gopeed 的 2.0 tag 目前仍宣告未帶 `/v2` 的 Go module 路徑，因此 Go 會將官方 tag 解析為 pseudo-version。這是 Go 的相依套件紀錄；不用在建置腳本手填上游 commit。對應的官方名稱記錄在 `core/upstream.json`，建置時寫入核心版本資訊。

一般 `build.ps1` 使用已記錄的版本，不會在建置中途切換核心。CI 手動執行時可選 `locked`、`preview` 或 `stable`；push／PR 檢查使用 `locked`。要保存更新，請提交：

```powershell
git add core/go.mod core/go.sum core/upstream.json
git commit -m "build: follow Gopeed release"
```

## 驗證

`build.ps1 -Test` 會執行 Harbor 核心測試、瀏覽器 host 測試、前端協定檢查及 Release win-x64 publish。核心整合測試使用獨立資料目錄和本機 HTTP／Torrent fixture，涵蓋選檔、暫停續傳、取消保留檔案及排程重啟恢復。

新增版本若改變 API、任務欄位或設定結構，調整 Harbor 的 API 模型和呼叫端，再重跑上述檢查。原生版面、瀏覽器喚醒與真實站台下載仍需互動驗證；編譯成功不能替代這些驗收。

需要回復時，從最後通過驗證的 Harbor commit 取回相依套件紀錄：

```powershell
git restore --source=<通過驗證的Harbor版本> -- core/go.mod core/go.sum core/upstream.json
pwsh -File scripts/build.ps1 -Test
```

## 發行原始碼

`source.ps1` 從 Go module cache 取得與建置相同的官方核心，在 `third_party/gopeed/` 附上完整 module 來源和授權。`SOURCE-REVISION.txt` 記錄 Harbor revision、官方 release、Go module 版本與 module checksum。

原始碼包預設仍使用官方 module。若要修改包內的核心做本機研究，可在包的 `core/` 目錄執行 `go mod edit -replace github.com/GopeedLab/gopeed=../third_party/gopeed`；正式維護請回到官方 module，避免累積核心分支差異。
