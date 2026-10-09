# Harbor

繁體中文 · [English](README.md)

使用 WinUI 3 與官方 [Gopeed](https://github.com/GopeedLab/gopeed) 下載引擎的 Windows 原生下載管理員。Harbor 維護 Windows 介面、系統匣、瀏覽器轉交與排程；下載協定及儲存使用 Gopeed Go module。

## 下載

[Harbor 0.10.0 Windows x64](https://github.com/tavricccc/harbor/releases/tag/v0.10.0) 預覽版。

- [安裝器](https://github.com/tavricccc/harbor/releases/download/v0.10.0/Harbor-Setup-0.10.0-x64.exe)
- [完整原始碼](https://github.com/tavricccc/harbor/releases/download/v0.10.0/Harbor-Source-0.10.0.zip)
- [SHA-256 校驗碼](https://github.com/tavricccc/harbor/releases/download/v0.10.0/SHA256SUMS.txt)

支援 Windows 10 1809 以上，建議使用 Windows 11。安裝器包含執行環境，安裝到使用者目錄；升級會保留下載與設定。

## 功能

- HTTP／HTTPS 多連線下載、暫停續傳；BitTorrent、magnet 與 eD2k。
- Gopeed 官方瀏覽器擴充套件接管下載。
- 批次連結、搜尋篩選、稍後下載與一次性排程。
- 視窗內展開下載選項與連線進度。
- 繁體中文、英文與簡體中文，以及 Windows 明暗主題。

預設開啟登入啟動，分類儲存預設關閉。關閉視窗後下載仍會繼續；從系統匣結束可停止背景核心。

## 文件

- [使用指南](docs/zh-TW/usage.md)
- [開發與上游更新](docs/zh-TW/development.md)
- [更新紀錄](CHANGELOG.zh-TW.md)
- [參與開發](CONTRIBUTING.md)

## 建置

Windows 需安裝 PowerShell 7、.NET 10 SDK、Go 1.27 與 Inno Setup 6：

```powershell
pwsh -File scripts/build.ps1 -Test
pwsh -File scripts/package.ps1 -SkipBuild
```

輸出位於 `artifacts/`，原始碼維護於 `winui-native` 分支。使用 [GPL-3.0](LICENSE) 授權。
