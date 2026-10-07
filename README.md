<img src="src/Gopeed.Native/Assets/Harbor.png" width="72" height="72" alt="Harbor 圖示">

# Harbor

![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Go](https://img.shields.io/badge/Go-00ADD8?logo=go&logoColor=white)
![License GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-22C55E)

Windows 原生下載管理員。使用 WinUI 3 介面與 [Gopeed](https://github.com/GopeedLab/gopeed) 下載引擎，提供繁體中文介面、瀏覽器下載接管、稍後下載與定時開始。

Harbor fork 自 Gopeed，是社群維護的獨立專案，並非 Gopeed 官方發行版。

## 下載

**[下載 Harbor 0.7.0](https://github.com/tavricccc/harbor/releases/tag/v0.7.0)** · Windows x64 預覽版

| 檔案 | 用途 |
| --- | --- |
| [安裝版](https://github.com/tavricccc/harbor/releases/download/v0.7.0/Harbor-Setup-0.7.0-x64.exe) | 一般使用者選這個。包含執行環境，安裝到使用者目錄，不需管理員權限。 |
| [Portable](https://github.com/tavricccc/harbor/releases/download/v0.7.0/Harbor-Portable-0.7.0-x64.zip) | 完整解壓縮後執行 `Gopeed.Native.exe`。 |
| [完整原始碼](https://github.com/tavricccc/harbor/releases/download/v0.7.0/Harbor-Source-0.7.0.zip) | 包含此版本使用的 Gopeed 核心。 |
| [SHA-256](https://github.com/tavricccc/harbor/releases/download/v0.7.0/SHA256SUMS.txt) | 下載檔案校驗碼。 |

支援 Windows 10 1809 以上及 Windows 11；建議 Windows 11。Windows 10 尚未完成實機驗證。從舊版直接執行安裝包即可升級，任務、設定與下載檔案會保留。

## 功能

- HTTP／HTTPS 多連線下載、暫停與中斷續傳。
- BitTorrent、magnet 與 eD2k；Torrent 支援挑選內含檔案。
- Gopeed 官方瀏覽器擴充套件接管下載。
- 多行連結批次加入、搜尋、排序與多選操作。
- 稍後下載及一次性的開始排程。
- 下載分類、代理設定與 Gopeed 擴充功能。
- 隨 Windows 切換明暗主題，關閉主視窗後可繼續下載。

目前排程需背景核心運行；不會喚醒睡眠或關機的電腦。到期項目會在核心下次啟動時補執行。多個命名佇列、週期排程與全域限速尚未提供。

## 瀏覽器接管

先在要使用的瀏覽器安裝 **Gopeed 官方擴充套件**：

| Chrome | Edge | Firefox |
| --- | --- | --- |
| [Chrome Web Store](https://chromewebstore.google.com/detail/gopeed/mijpgljlfcapndmchhjffkpckknofcnd) | [Edge Add-ons](https://microsoftedge.microsoft.com/addons/detail/dkajnckekendchdleoaenoophcobooce) | [Firefox Add-ons](https://addons.mozilla.org/firefox/addon/gopeed-extension) |

1. 確認擴充套件已啟用。
2. 開啟 Harbor 的「瀏覽器接管」引導。安裝版會註冊本機接管；Portable 版按「啟用本機接管」。
3. 在 Gopeed 擴充套件設定中關閉「遠端下載」，再嘗試下載一個檔案。出現 Harbor 確認視窗就表示接管生效。

本機接管不用填伺服器位址或 Token。詳細設定與排除問題見 [瀏覽器接管指南](docs/browser-integration.md)。

## 使用

按「貼上網址」或 Ctrl+V 加入連結；每行一個可批次加入。按「新增下載」可調整位置、檔名與分類，再選擇立即開始、稍後下載或排程。

Torrent 可從工具列選取，也能拖進主視窗。清單右側操作隨狀態切換，下載中可暫停、失敗可重試，完成後可開啟檔案。移除任務預設保留檔案。

HTTP 來源失效時，可修改網址，或選「用下一次瀏覽器連結更新來源」接續原任務。

| 快捷鍵 | 操作 |
| --- | --- |
| Ctrl+N / Ctrl+V | 新增下載 / 貼上連結或 Torrent |
| Ctrl+F / F5 | 搜尋 / 重新整理 |
| Ctrl+A / Delete | 在清單全選 / 移除選取任務 |
| Ctrl+Enter / Esc | 在確認視窗檢查或開始 / 取消 |

## 資料與解除安裝

任務、設定與 API Token 放在 `%LOCALAPPDATA%\GopeedNative`。解除安裝會移除程式與整合註冊，保留任務資料和下載檔案。回報問題時，請勿附上 Token、Cookie 或私人下載連結。

## 建置

需要 Windows、PowerShell 7、Go 1.27 與 .NET 10 SDK。安裝包另需 Inno Setup 6。

```powershell
 git clone --branch winui-native --recurse-submodules https://github.com/tavricccc/harbor.git
 cd harbor
 pwsh -File scripts/build.ps1 -Test
 pwsh -File scripts/package.ps1 -SkipBuild
 pwsh -File scripts/source.ps1
```

`core/` 是背景服務；`src/Gopeed.Native/` 是 WinUI 前端；`upstream/` 固定 Gopeed 核心版本。0.7.0 使用 Gopeed 2.0 開發版本 `224b4880871f8d7a87c16a3d4daca31546f0ca80`。參與方式見 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 致謝與授權

感謝 [GopeedLab 與 Gopeed 貢獻者](https://github.com/GopeedLab/gopeed/graphs/contributors)開源並持續維護下載引擎。Harbor 的核心能力建立在 Gopeed 之上；WinUI 3 介面另行實作。

依 [GPL-3.0](LICENSE) 發布，保留上游授權與來源。Release 附加的完整原始碼包含 submodule；GitHub 自動產生的 Source code ZIP 不包含，請下載附加檔或遞迴 clone。
