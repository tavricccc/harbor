# 使用指南

[English](../usage.md) · [首頁](../../README.zh-TW.md)

## 下載

按「新增下載」或 Ctrl+N，貼上連結並選擇目錄。每行一個連結可批次加入；Ctrl+V 與拖放可帶入連結或 Torrent 檔案。

先檢查檔案，再按「開始下載」；Torrent 可挑選內含檔案。左下箭頭在視窗內展開選項，HTTP 標頭與進階區域預設收合，超出展開區的內容可捲動。

進度視窗的來源網址可點擊開啟、右鍵複製。左下箭頭顯示各連線進度與取樣速度。「取消」會移除下載任務、保留部分檔案；關閉視窗後任務仍會繼續。

## 待下載與排程

「稍後下載」只保存請求，「排程」可指定一次性的開始時間。從「更多」開啟待下載清單，可立即開始、重試或改時間。

排程需要背景核心執行。未執行的到期排程會在 Harbor 下次啟動時開始，不會喚醒睡眠或關機的電腦。

## 設定

按儲存套用修改，取消則放棄。「介面」可調整語言、主題與登入啟動；語言在視窗重新開啟後套用。啟用分類儲存後，新增下載才會顯示分類目錄。

要完全停止 Harbor，從系統匣結束。解除安裝會清除程式設定、任務紀錄與整合註冊，保留下載檔案。

## 瀏覽器接管

在瀏覽器安裝 Gopeed 官方擴充套件：

- [Chrome](https://chromewebstore.google.com/detail/gopeed/mijpgljlfcapndmchhjffkpckknofcnd)
- [Edge](https://microsoftedge.microsoft.com/addons/detail/dkajnckekendchdleoaenoophcobooce)
- [Firefox](https://addons.mozilla.org/firefox/addon/gopeed-extension)

Harbor 安裝時會註冊本機接管。如果下載沒有交給 Harbor，到「設定 → 連線」按「修復註冊」，再確認瀏覽器擴充套件已啟用。擴充套件本身的設定見[官方文件](https://github.com/GopeedLab/browser-extension)。
