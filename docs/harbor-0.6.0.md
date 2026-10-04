# Harbor 0.6.0

以 [IDM 佇列與排程](https://help.internetdownloadmanager.com/support/idm-scheduler/idm_scheduler.html)、[AB Download Manager 官方文件](https://abdownloadmanager.com/docs) 的操作需求作為比較，本次補上下載前保存與指定時間開始，同時改善下載確認視窗的前景啟用。

| 操作 | 目前實作 |
| --- | --- |
| 瀏覽器下載接管 | 官方擴充套件，確認前不建立下載 |
| 確認視窗前景 | 顯示 HWND 後明確設定置頂；跨程序轉交 foreground permission |
| 稍後下載 | 單一與批次請求保存在核心資料目錄，保存階段不開始檔案傳輸 |
| 定時開始 | 一次性日期與時間，核心執行期間準時開始，重啟補執行到期項目 |
| 修改排程 | 右鍵調整日期／時間或改為稍後下載 |
| 排程失敗 | 保留錯誤與項目，手動重試；不無限自動重試 |
| 同時下載數 | 沿用 Gopeed `maxRunning`，在設定／下載調整 |
| 批次加入 | 多行連結、多選 Torrent、拖放 Torrent |
| 剪貼簿 | Ctrl+V／貼上網址接受連結及複製的 Torrent 檔案 |
| HTTP 中斷續傳 | 沿用既有來源更新、暫停、繼續與重試 |
| BitTorrent | 本機 Torrent、magnet、內含檔案選取、Tracker、做種與節點資訊 |

本次沒有加入多個命名佇列、週期性排程、全域頻寬上限、瀏覽器影片嗅探，也不宣稱與 IDM 或 AB 的所有功能相同。

## 驗證

- 核心測試包含 HTTP 傳輸／中斷續傳，以及排程保存、重載、到期、失敗保留、手動重試與權限。
- BT 整合測試建立本機種子與 Tracker，解析雙檔 Torrent，只傳輸所選檔案並比對 SHA-256；以獨立程序測試核心生命週期，程序退出後清理檔案。
- WinUI Release 編譯與自包含 publish、Inno Setup／Portable 打包。
- 原生前景焦點與完整視覺尚需互動桌面實測；WinUI `Activate()`、Win32 foreground permission 與明確置頂已接入啟動流程，但編譯不代表瀏覽器前景操作已驗收。

## 品牌資產

品牌名稱 Harbor；深墨綠 `#123D42`、海沫綠 `#64D9BF`、白 `#F4FAF9`。可編輯 SVG 與 `scripts/create_icon.py` 產生的 ICO／PNG 是正式資產，ImageGen 的四格設計板只作方向參考。

設計板使用內建 ImageGen，提示為：Harbor 原生 Windows 下載器，港口承接匯入檔案，簡潔 U 型碼頭與下行緞帶，深墨綠／海沫綠，2×2 品牌板，標記、幾何構造、Windows 圖示與標題列應用，避免一般藍底下載箭頭。

```powershell
python -m pip install -r scripts/requirements.txt
python scripts/create_icon.py
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
```

為保留舊版資料及官方擴充套件相容性，安裝目錄、`Gopeed.Native.exe`、`%LOCALAPPDATA%\GopeedNative`、Registry 識別碼及 `gopeed://` 沿用原值；對外名稱、捷徑、圖示與新版安裝包使用 Harbor。
