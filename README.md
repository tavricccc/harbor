<img src="src/Gopeed.Native/Assets/Harbor.png" width="72" height="72" alt="Harbor 圖示">

# Harbor

Harbor 是 Windows 下載管理員，使用 WinUI 3 介面與 [Gopeed](https://github.com/GopeedLab/gopeed) 下載引擎。支援瀏覽器下載接管、HTTP 中斷續傳、BitTorrent，以及稍後下載與定時開始。介面使用繁體中文，隨 Windows 切換明暗主題。

這是社群維護的 Gopeed fork，並非 Gopeed 官方發行版。原名 Gopeed Native，從 0.6.0 起改名為 Harbor。

## 下載與安裝

目前版本：[Harbor 0.6.0](https://github.com/tavricccc/harbor/releases/tag/v0.6.0)，Windows x64 預覽版。

| 檔案 | 用途 |
| --- | --- |
| [Harbor-Setup-0.6.0-x64.exe](https://github.com/tavricccc/harbor/releases/download/v0.6.0/Harbor-Setup-0.6.0-x64.exe) | 安裝版。建立開始功能表捷徑，註冊瀏覽器接管與下載協定，不需管理員權限。 |
| [Harbor-Portable-0.6.0-x64.zip](https://github.com/tavricccc/harbor/releases/download/v0.6.0/Harbor-Portable-0.6.0-x64.zip) | 完整解壓縮後執行 `Gopeed.Native.exe`。 |
| [Harbor-Source-0.6.0.zip](https://github.com/tavricccc/harbor/releases/download/v0.6.0/Harbor-Source-0.6.0.zip) | 完整原始碼，含固定版本的上游核心。 |
| [SHA256SUMS.txt](https://github.com/tavricccc/harbor/releases/download/v0.6.0/SHA256SUMS.txt) | 核對下載檔案的 SHA-256。 |

已在 Windows 11 x64 建置與安裝。安裝包包含 .NET 與 Windows App SDK runtime；Windows 10 尚未完成實機驗證。

從 Gopeed Native 升級可直接執行安裝包，原有任務、設定與下載檔案會保留。程式檔名仍為 `Gopeed.Native.exe`，資料仍放在 `%LOCALAPPDATA%\GopeedNative`。

## 開始下載

在主視窗按「貼上網址」或 Ctrl+V，貼上 HTTP、HTTPS、magnet 或 eD2k 連結。一次貼上多行連結可批次加入。「新增下載」可調整儲存位置、檔名、分類和這次下載的設定。

Torrent 檔案可用工具列的「開啟 Torrent」加入，也能拖進主視窗，或在檔案總管複製後按 Ctrl+V。選檔視窗支援多選；單一 Torrent 檢查完成後，可勾選要下載的內含檔案。Tracker、做種條件與連接埠在設定的「下載」分頁調整。

清單每筆右側的主要操作會隨狀態切換：下載中可暫停、失敗可重試，完成後可開啟檔案。支援搜尋、排序、Ctrl／Shift 多選，以及批次暫停、繼續和移除。移除任務時預設保留檔案。

HTTP 來源失效時，暫停後可修改網址；也可選「用下一次瀏覽器連結更新來源」，等待瀏覽器送來新連結後接續原任務。

## 稍後下載與排程

確認視窗的「稍後下載」會保存請求，現在不開始傳輸。「排程」可指定一次性的開始日期與時間。

保存後會開啟主清單的「待下載／排程」篩選。按播放按鈕可立即開始，右鍵「調整排程」可改時間，或改回手動開始。「全部繼續」會開始手動待下載項目，有指定時間的項目仍依排程執行。

關閉介面後，背景核心會繼續下載和處理排程。電腦關機期間到期的項目，會在下次核心啟動時補執行。排程啟動失敗會保留項目與錯誤原因，修正後可手動重試。

目前排程是單次開始；多個命名佇列、週期排程和全域限速尚未提供。

## 瀏覽器接管

安裝 Harbor 與 [Gopeed 官方擴充套件](https://github.com/GopeedLab/browser-extension)，並在擴充套件關閉「遠端下載」。本機接管不需填伺服器位址或 Token，安裝包會替 Chrome、Edge 和 Firefox 註冊 Native Messaging host。

瀏覽器送來的下載會先開啟獨立確認視窗。按「開始下載」才建立任務，也可選稍後下載或排程；取消不會下載。確認窗會置頂，開始傳輸後解除置頂，顯示進度與完成操作。

Portable 版可在設定的「連線」分頁按「啟用瀏覽器下載接管」註冊。啟用後請保留 Portable 資料夾位置。其他 Gopeed 安裝若改寫接管註冊，也可用這個按鈕重新啟用。

Harbor 接受官方 `gopeed://` 連結。遠端 HTTP 連線設定仍在「連線」分頁，供需要手動設定位址與 Token 的使用者使用。

## 快捷鍵

| 按鍵 | 操作 |
| --- | --- |
| Ctrl+N | 新增下載 |
| Ctrl+V | 在主視窗貼上連結或 Torrent 檔案 |
| Ctrl+F | 搜尋下載 |
| F5 | 重新整理 |
| Ctrl+A | 在清單選取全部 |
| Delete | 移除選取的任務 |
| Ctrl+Enter | 在確認視窗檢查或開始下載 |
| Esc | 取消下載確認 |

## 資料與解除安裝

任務、偏好與 API Token 位於 `%LOCALAPPDATA%\GopeedNative`。核心只監聽本機介面，API 使用 Token 驗證。回報問題時，請勿附上 Token、session、Cookie 或含私人連結的請求檔。

解除安裝會停止核心、移除整合註冊，並還原安裝前的瀏覽器 host；任務資料與下載檔案會保留。若要清除任務資料，先結束核心，再刪除上述資料夾。

## 開發

準備 Windows、PowerShell 7、Go 1.27 與 .NET 10 SDK。製作安裝包另需 Inno Setup 6。

```powershell
git clone --branch winui-native --recurse-submodules https://github.com/tavricccc/harbor.git
cd harbor
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
pwsh -File scripts/source.ps1
```

程式碼維護在 `winui-native` 分支，`main` 保留上游內容。下載引擎固定在 Gopeed v1.9.3，submodule commit 為 `a5cd53f94c18ac65add684b1113fa5f0b47cc4da`。

`core/` 維護背景服務、瀏覽器請求與排程；`src/Gopeed.Native/` 是 WinUI 前端；`tests/Gopeed.ProtocolChecks/` 檢查協定、HTTP 標頭與狀態操作。建置產物放在 `artifacts/`。重新建置前，請關閉從該目錄執行的前端、核心和瀏覽器 host。

圖示原稿是 `src/Gopeed.Native/Assets/Harbor.svg`。要重產 Windows 圖示，先執行 `python -m pip install -r scripts/requirements.txt`，再執行 `python scripts/create_icon.py`。介面規則見 [DESIGN.md](DESIGN.md)，參與方式見 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 驗證狀態

0.6.0 已通過核心測試、ProtocolChecks、WinUI Release publish、安裝包與 Portable 打包。測試包含 HTTP 暫停與重啟續傳、排程保存與到期處理，以及本機 Tracker／種子的 BT 選檔傳輸和 SHA-256 比對。打包後也檢查了核心重啟、排程修改與移除。

瀏覽器前景焦點、新版原生視覺、Narrator、高對比、公開 BT 網路和 eD2k 傳輸尚待實機驗收。此版仍列為預覽版。詳細範圍見 [0.6.0 維護紀錄](docs/harbor-0.6.0.md)；遠端建置結果以 [GitHub Actions](https://github.com/tavricccc/harbor/actions) 為準。

## 授權

[GPL-3.0](LICENSE)。下載引擎來自 GopeedLab/gopeed，保留上游授權與來源。Release 附加的完整 Source ZIP 包含 submodule；GitHub 自動產生的 Source code ZIP 不包含，請下載附加檔或遞迴 clone。
