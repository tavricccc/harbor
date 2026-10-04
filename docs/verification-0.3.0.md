# Gopeed Native 0.3.0 驗證紀錄

日期：2026-10-01。環境：Windows 11 x64、PowerShell 7、Go 1.27.0、.NET 10、自包含 WinUI 3、Inno Setup 6.7.3。

## 建置與 CI

- 本機 `scripts/build.ps1 -Test` 通過：Go 核心建立／暫停／重啟續傳、確認入口、單一服務 ownership、鏡像轉換、官方 native host 訊息；C# 協定、UTF-8、HTTP 標頭 CR／LF／CRLF 和狀態操作檢查通過。
- 原生前端 Release build 為 0 個警告、0 個錯誤；完整自包含部署含前端、Go 核心、Native Messaging host、圖示與平台 runtime。
- Windows CI 的 build、tests、Inno installer、portable ZIP 及 artifact upload 均通過：[功能版](https://github.com/tavricccc/harbor/actions/runs/36803181218)、[最後確認窗修正版](https://github.com/tavricccc/harbor/actions/runs/36804011217)。這與本機安裝測試分開記錄。
- 可執行程式對應程式碼 commit `00de39aac468603631d5ce0a139b9829b96dee22`；後續文件與截圖 commit 不改程式碼。上游仍為 v1.9.3 固定 submodule。

## 實際安裝、解除安裝與資料

- 已由 installer 升級 0.2.0 → 0.3.0，退出碼 0；登錄的 DisplayVersion 是 0.3.0。
- 安裝目錄的 `Gopeed.Native.dll`、`Engine/gopeed-core.exe`、`Engine/gopeed-browser-host.exe` 和 `Assets/AppIcon.ico` 的 SHA-256 均與本機 portable 產物相符。
- Chrome、Edge、Firefox 的 `com.gopeed.gopeed` 登錄皆指向有效 manifest 與部署後的 host EXE；Chrome／Edge 允許兩個官方商店 ID，Firefox 允許官方套件 ID。
- 實際解除安裝退出碼 0：前端移除、背景服務停止、自己的預設應用程式能力移除；三個瀏覽器 host 還原為安裝前的值。
- 下載資料庫、偏好與 Token 保留，Token hash 未變；重新安裝退出碼 0，原有三筆完成任務的 ID 完整保留。
- QA 任務只在獨立工作資料夾下載。已移除本輪自己的任務及擴充功能、還原原有設定與偏好，沒有刪除使用者下載檔案。

## 官方擴充套件契約與確認流程

以官方 1.1.5 原始碼使用的 4-byte little-endian 長度、JSON envelope、`params` base64 字串傳送請求，直接啟動安裝後的 Native Messaging host：

- ping、base64 create、forward 及重複 query 參數通過；外部 URL forward 和不合法 base64 會被拒絕。
- create 可開啟原生確認窗，確認前 API 任務數不變；取消後仍為原有三筆任務。
- 最後安裝版的確認窗顯示大小、位置與檔名，沒有內嵌 dialog 白邊；來源大小放在進階選項前。
- 按開始後同一個視窗變為進度／完成，主要操作為第一個「開啟檔案」，第二個「在資料夾中顯示」；UIA 確認均可見且啟用。
- 主清單關閉時也能完成獨立下載。最後版本的文字檔與來源 SHA-256 相同。
- 最後一輪曾抓到載入前 reparent 控制項的例外；已改為 XAML 宣告位置，重新打包、安裝並重新測試上述確認／取消／下載流程通過。

這是安裝註冊與官方訊息契約的實測；本輪沒有在 Chrome／Edge／Firefox 的實際擴充套件 options 頁重新執行整條點擊下載流程，也沒有改寫瀏覽器儲存資料。原來的 HTTP 遠端入口仍保留；本機接管使用官方預設遠端模式關閉的設定。

## 下載、來源更新與背景工作

- HTTP 請求保留 Sec-Ch-Ua 引號、Referer 與 Cookie；測試只使用自己的 fixture 值。
- 64 MiB ZIP 開始、完成、解壓縮皆正常；解壓後 `payload.bin` 的 SHA-256 相同，原 ZIP 只在解壓縮成功後移除。完成主要操作改為「開啟解壓縮資料夾」。
- 暫停 HTTP 任務，再以瀏覽器新請求更新來源；原 ID 保留，已下載的 4,014,080 bytes 保留，續傳完成至 67,108,984 bytes，最後檔案與來源相同。
- 多行批次建立與批次 pause／continue 通過；另以兩筆實際傳輸中的 ZIP 檢查兩筆同時暫停，並在原生清單搜尋、Ctrl+A 多選後，用「繼續選取的下載」接續。兩筆任務完成後，實際儲存的檔案 SHA-256 都與來源相同。
- 開啟時自動繼續 paused 任務通過；Tracker 訂閱在背景服務重啟後自動取得兩筆，合併自訂一筆成三筆。Webhook 測試由本機接收器實際收到。
- 同一資料夾的 named mutex 可阻止第二個服務實例，釋放後可再次啟動。

## 設定與擴充功能

- UIA 輸入 NumberBox 並用 Enter 提交，再按儲存；API `config.maxRunning=256`，以整數保存，不因 JSON 小數型別而寫入失敗。直接透過 UIA 改內層文字而未提交，不等於 NumberBox 值已更新。
- 商店取得官方資料，顯示搜尋、排序、安裝與詳細介紹入口；來源安裝區可直接展開。
- 自有本機擴充 fixture 以一般安裝複製，`devMode=false`；API 設定、停用均保存。原生設定 dialog 修改文字為「中文保存」後，API 讀回相同值；測試後解除安裝 fixture。
- 分類、進階選項、BT／eD2k、代理、解壓縮、Torrent、腳本、鏡像等入口經來源與 API 契約核對及編譯，未對每一組欄位排列做全組合測試。

## 圖片與審查

- 最後安裝版：[主清單](images/native-main.png)、[確認窗](images/native-confirmation-0.3.0.png)、[完成窗](images/native-completed-0.3.0.png)。
- 同版較早建置：[設定](images/native-settings-0.3.0.png)、[商店](images/native-store-0.3.0.png)、[解壓完成](images/native-extracted-0.3.0.png)。這些畫面的相關程式碼沒有被最後確認窗修正變更。
- 保存的 PNG 已用本機圖片工具開啟確認；未使用空白或停留在前一畫面的擷取圖作為對應頁面的證據。
- Impeccable finish reviewer 對新流程的來源審查沒有確認的 P0／P1；documenter 更新原生設計資料。此來源審查不代表所有畫面尺寸、輸入方式和系統都已實測。

## 尚未驗證／不承諾

真實 BT／eD2k 網路、第三方擴充功能的網站解析、Windows 分享目標、實際通知顯示政策、登入 Windows 後的啟動、Narrator、高對比、跨應用程式拖放和 Windows 10 尚未完成端到端驗證。淺色下的上述頁面有有效截圖；這次新增頁面的每個深色及最小尺寸狀態尚未逐一拍攝。沒有與官方版本相同工作量的 RAM 對照，不宣稱節省比例。

原版差異與入口清單見 [功能對照](feature-parity.md)。
