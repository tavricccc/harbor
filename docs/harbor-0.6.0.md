# 0.6.0 維護紀錄

Harbor 0.6.0 加入稍後下載與一次性排程，更新品牌圖示，並補上確認視窗顯示後的置頂與前景啟用流程。使用方式見 [README](../README.md)，版本變更見 [CHANGELOG](../CHANGELOG.md)。

## 排程資料與啟動

待下載請求放在核心資料目錄的 `deferred-downloads.json`。保存請求不會開始檔案傳輸；到了指定時間，或使用者按「立即開始下載」，才交給 Gopeed 建立任務。

背景核心每秒檢查到期項目。介面關閉後仍會執行；電腦關機期間到期的項目，下次核心啟動時補執行。啟動失敗會保留錯誤與請求，等待手動重試。

每筆請求帶有 `harborDeferredId`。如果核心在任務建立後、待下載項目移除前中斷，下次交接會先找同一識別碼的任務，避免重複建立。停止核心時，先停止排程工作，再保存下載狀態。

「全部繼續」包含手動待下載項目，但略過有指定時間的排程。要提前開始排程，選取該項目再按播放。

## 升級與相容性

對外名稱、捷徑、圖示與 Release 檔案從 0.6.0 起使用 Harbor。以下識別碼沿用 Gopeed Native 的值，讓既有下載與官方擴充套件能繼續使用。

| 項目 | 沿用的值 |
| --- | --- |
| 執行檔 | `Gopeed.Native.exe` |
| 安裝目錄 | `%LOCALAPPDATA%\Programs\Gopeed Native` |
| 任務與偏好 | `%LOCALAPPDATA%\GopeedNative` |
| 協定 | `gopeed://` |
| Native Messaging host | `com.gopeed.gopeed` |

安裝包會將開始功能表群組改為 Harbor，並移除舊名稱的捷徑。程式內的更新檢查、專案首頁與授權連結改指向 `tavricccc/harbor`。

瀏覽器若仍持有舊版 host，可能阻擋安裝包覆寫檔案。先關閉瀏覽器再重試安裝；下載核心由安裝包正常停止，任務資料會保留。

## 已完成的檢查

- 核心測試：HTTP 建立、暫停、重啟續傳、檔案雜湊、移除保留檔案，以及排程保存、重載、到期、失敗保留、重試與權限檢查。
- BT 本機整合：建立本機種子與 Tracker，解析雙檔 Torrent，只下載所選檔案，再比對 SHA-256。測試使用獨立核心程序，退出後清理檔案。
- ProtocolChecks：協定參數、UTF-8、標頭 CR／LF／CRLF、Cookie／Referer／Sec-Ch-Ua 內容與狀態操作。
- WinUI Release win-x64 publish、Inno Setup、Portable ZIP 與完整 Source ZIP。
- 打包後核心的 HTTP 檢查：稍後保存不開始傳輸、正常停止與重啟、排程保存、修改與移除。本機升級後既有完成紀錄保留。

瀏覽器前景焦點與新版原生視覺尚未完成互動桌面驗收；公開 BT 網路、eD2k、Narrator、高對比與 Windows 10 也仍待實機測試。編譯與本機核心測試不代表這些流程已通過。

## 圖示

正式圖示以 U 型碼頭承接匯入檔案為概念。固定色彩為深墨綠 `#123D42`、海沫綠 `#64D9BF` 與白 `#F4FAF9`，介面控制項仍使用 Windows 主題資源。

`src/Gopeed.Native/Assets/Harbor.svg` 是可編輯原稿；`scripts/create_icon.py` 產生各尺寸 ICO／PNG。`Harbor-brand-direction.png` 是內建 ImageGen 產生的設計參考板，正式圖示由幾何程式繪製。生成提示使用 Harbor、港口與匯入檔案、U 型碼頭及下行緞帶、深墨綠／海沫綠，以及標記、幾何構造、Windows 圖示與標題列的四格應用。
