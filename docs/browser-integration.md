# 瀏覽器下載接管

Harbor 負責保存與下載檔案，Gopeed 官方擴充套件負責把瀏覽器的下載請求交給 Harbor。擴充套件需在瀏覽器另行安裝。

## 安裝擴充套件

用要接管下載的瀏覽器開啟商店，加入 Gopeed：

- [Chrome Web Store](https://chromewebstore.google.com/detail/gopeed/mijpgljlfcapndmchhjffkpckknofcnd)
- [Microsoft Edge Add-ons](https://microsoftedge.microsoft.com/addons/detail/dkajnckekendchdleoaenoophcobooce)
- [Firefox Add-ons](https://addons.mozilla.org/firefox/addon/gopeed-extension)

以上連結來自 [Gopeed 官方擴充套件 README](https://github.com/GopeedLab/browser-extension)。安裝完成後，在瀏覽器的擴充套件管理頁確認 Gopeed 已啟用。

## 連接 Harbor

在 Harbor 主視窗按工具列的更多選單，選「瀏覽器接管」，或到設定的「連線」分頁。0.6.1 首次開啟主視窗也會顯示「安裝與設定」提示；關閉提示後，入口仍會保留。

Harbor 僅提供安裝版，安裝時會自動註冊本機 Native Messaging host，啟動時也會補上缺少的註冊，不需手動啟用。

引導顯示「Harbor 的本機接管已註冊」時，代表 Chrome、Edge 和 Firefox 的 host 註冊都指向目前這份 Harbor。它不會檢查或替瀏覽器安裝擴充套件，也不代表一次下載已成功接管。

開啟 Gopeed 擴充套件設定，關閉「遠端下載」。一般本機接管不需填入伺服器位址或 Token。

## 試一次下載

回到網站，點一個檔案的下載連結。出現 Harbor 確認視窗，就表示請求已交給 Harbor。按「開始下載」才開始傳輸；也可選稍後下載或排程，取消則不建立下載任務。

開始後，確認視窗解除置頂並顯示進度。完成時可開啟檔案或所在資料夾。

## 沒有出現確認視窗

先確認 Gopeed 擴充套件已啟用，且「遠端下載」關閉。重新開啟 Harbor 自動補上註冊，再重新開啟瀏覽器並測試。

若同時安裝官方 Gopeed 或其他 fork，它們可能改寫共用的 `com.gopeed.gopeed` host 註冊。要改由 Harbor 接管，重新開啟 Harbor 即可。

網站資源嗅探由官方擴充套件處理，能否解析仍取決於網站與擴充套件的能力。手動貼上 HTTP、magnet 或 Torrent 檔案可獨立使用 Harbor。
