---
name: Gopeed Native
description: 對齊 Downlism 原生下載器的單列清單、分頁設定與內容量測下載視窗
colors:
  icon-blue: "#0067C0"
  icon-white: "#FFFFFF"
typography:
  title:
    fontFamily: Segoe UI Variable Display, Microsoft JhengHei UI
    fontSize: 20dip
    fontWeight: 600
  subtitle:
    fontFamily: Segoe UI Variable Display, Microsoft JhengHei UI
    fontSize: 16dip
    fontWeight: 600
  body:
    fontFamily: Segoe UI Variable Text, Microsoft JhengHei UI
    fontSize: 14dip
    fontWeight: 400
  body-strong:
    fontFamily: Segoe UI Variable Text, Microsoft JhengHei UI
    fontSize: 14dip
    fontWeight: 600
  caption:
    fontFamily: Segoe UI Variable Text, Microsoft JhengHei UI
    fontSize: 12dip
    fontWeight: 400
  download-name:
    fontFamily: Segoe UI Variable Text, Microsoft JhengHei UI
    fontSize: 14dip
    fontWeight: 400
  kind:
    fontFamily: Segoe UI Variable Text, Microsoft JhengHei UI
    fontSize: 11dip
    fontWeight: 400
rounded:
  control: 4dip
  overlay: 8dip
spacing:
  action: 2dip
  row: 6dip
  inline: 8dip
  group: 10dip
  control: 12dip
  form: 14dip
  section: 16dip
  page: 20dip
  table-inset: 24dip
components:
  button-primary:
    rounded: "{rounded.control}"
  row-action-button:
    backgroundColor: transparent
    rounded: "{rounded.control}"
    padding: 8dip 6dip
  download-row:
    padding: 12dip 6dip
    height: 44dip
  download-size-column:
    width: 140dip
  download-progress-column:
    width: 152dip
  download-speed-column:
    width: 104dip
  download-actions-column:
    width: 100dip
  download-filter:
    width: 150dip
  download-sort:
    width: 140dip
  download-confirmation:
    width: 660dip
  download-progress:
    width: 660dip
  download-footer:
    padding: 24dip 14dip
  download-kind:
    rounded: "{rounded.control}"
    padding: 6dip 2dip
---

# Design System: Gopeed Native

## Overview

**Creative North Star: "Windows 下載工作佇列"**

沿用使用者指定的 Downlism 原生下載器，將命令、檔名、傳輸量、進度／狀態、速度與列操作放在熟悉的 Windows 版面。主清單直接開始工作，設定另開獨立視窗；來源與完整詳情按需開啟。

介面採 WinUI 3 的 `XamlControlsResources`、`ThemeResource` 與原生視窗標題列。Mica 提供背景，細線底列承載下載動作。下載確認與進度頁共用內容寬度，依可見內容量測高度；設定使用分頁、成組欄位與固定可達的儲存操作。

**Key Characteristics:**

- 原生系統標題列、Mica 與靠右的 CommandBar。
- 平面單列清單，依狀態顯示透明列操作，保留原生 hover、focus 與 disabled。
- 確認欄位並排；進度以檔名、位置、單行數據、進度條與狀態呈現。
- 設定另開六分頁視窗，進階下載選項放在底列箭頭 Flyout。
- 窄視窗隱藏大小欄，完整來源、路徑與檔案清單按需開啟。

本文件同步 2026-10-03 尚未發布的介面修改，安裝版本仍為已發布的 0.5.0。設計權威是 `C:/Users/Tavric/projects/downlism/src/Downlism.App` 的 MainWindow、NewDownloadWindow 與 SettingsWindow；既有 Downlism 截圖只供參考，網站版面不作為原生介面依據。SDK 控制項基準為 `Microsoft.WindowsAppSDK.WinUI` 2.3.9 的 `generic.xaml`。

本次已完成原始碼審查、Release win-x64 publish 與既有 ProtocolChecks；審查發現的關閉 InfoBar 殘留間距已修正，輸出位於 `artifacts/downlism-ui/`。沒有啟動或控制原生介面，也沒有本次有效原生截圖，因此尚未確認實際字級、對比、裁切或參考一致性。舊 Gopeed 截圖、空白截圖與歷史操作紀錄不列為本次視覺驗收。擴充功能頁本次未重做。

## Colors

介面以 WinUI 主題資源為準。前置資料只列出應用程式圖示真正固定的色彩，不能當作整個介面的固定配色。

- **系統重點色**：確認開始、完成開啟、重試、儲存及擴充功能安裝使用 `AccentButtonStyle`；進度條與焦點沿用平台資源。主清單列操作使用透明的 `RowActionButtonStyle`，依任務狀態顯示。
- **主要與次要文字**：主要文字沿用控制項前景；欄名、路徑、摘要與說明使用 `TextFillColorSecondaryBrush`。
- **背景與分隔**：下載內容透明；底列使用 `LayerFillColorDefaultBrush` 與 `DividerStrokeColorDefaultBrush`。CommandBar 背景透明，清單保留原生選取與 hover。
- **圖示藍與白**：只用於程式生成的下載箭頭圖示，按鈕不硬編碼成圖示藍。

**The Theme Resource Rule.** 沿用平台資源及完整狀態範本，不從截圖取色覆蓋控制項。所有 ContentDialog 經 NativeDialogs 同步視窗實際主題。

## Typography

內文、欄位與下載檔名使用 `ContentControlThemeFontFamily`；精簡標題使用 `HeadingFontFamily`。角色字級見前置資料；設定視窗的大標題沿用平台 `TitleTextBlockStyle`，不為主清單新增頁首。

| 角色 | 實作與用途 |
| --- | --- |
| title／subtitle | CompactPageTitleStyle／CompactSectionTitleStyle，頁標題及區段標題 |
| body／body-strong | 一般文字、欄位標籤與需要強調的資訊 |
| caption | SecondaryTextBlockStyle、進度文字、路徑與傳輸數據 |
| download-name／kind | 單行檔名與小型「檔案／BT／批次」標籤 |

清單與進度窗檔名使用 CharacterEllipsis 並提供 Tooltip。來源與詳細路徑可選取複製。列操作使用 14 DIP FontIcon；底列設定箭頭為 12 DIP；一般 NativeButtons 文字按鈕仍使用 16 DIP 圖示與 8 DIP 間距。空清單使用 56 DIP 應用程式圖示及平台 SubtitleTextBlockStyle。沒有自訂字距、行高或裝飾性圖示。

**The Compact Hierarchy Rule.** 精簡層級服務掃讀；主清單以命令列開始，狀態與摘要使用文字表達。

## Layout

所有尺寸以 DIP 表示，視窗建立時依 DPI 換算像素，截圖像素不直接作為 token。

- **主視窗**：預設整窗 1120 × 720 DIP、最小 780 × 520 DIP，使用系統標題列。主頁列距 12 DIP，命令區 Margin 為 12,8,12,0。CommandBar 依序為貼上、新增、全部暫停、全部繼續、設定；選取動作及其他命令放在 overflow。
- **篩選與清單**：篩選區左右 Margin 20 DIP、欄距 10 DIP；篩選與排序寬度引用前置資料，搜尋占剩餘寬度，清除已完成置右。表頭左右 Margin 引用 table-inset，欄距引用 control；表頭與列共用檔名／大小／進度／速度／操作欄寬。ListView 為 Extended 多選，Padding 為 8,0,8,12；列高與 padding 引用 download-row，操作間距引用 action。表格 Grid 可用寬度低於 860 DIP 時隱藏大小欄。摘要留在清單底部。
- **設定視窗**：預設 client 820 × 760 DIP，受工作區大小限制；最小整窗 720 × 560 DIP。內容 Padding 24,16,24,20，列距 16 DIP；分頁內容各自捲動，Padding 0,12,12,16，組間距 20 DIP。成對欄位等寬並排，欄距 20 DIP、內部間距 8 DIP。取消與儲存始終位於右下。
- **下載確認**：內容 Padding 24,20,24,16；52 DIP 標籤欄、16 DIP 欄距。可見欄位以 form 間距分開，可選分類／操作／檔案區隱藏後不保留空白。一般來源多行，高度 72–120 DIP；瀏覽器來源單行。檔案清單最大高 180 DIP。
- **下載進度**：內容 Padding 與確認相同，段距 12 DIP；檔名與類型標籤同列，位置在下，大小靠左、速度置中、剩餘時間靠右。底列使用 download-footer，按鈕間距 8 DIP。
- **下載窗量測**：確認與進度引用相同內容寬度；量測內容與 Footer 的 client height，最小 140 DIP、最高工作區高度減 48 DIP，不另加自訂標題列。量測後固定大小，狀態或欄位變化重新量測。確認期間置頂、不可最大化／最小化；開始後解除置頂並允許最小化。
- **詳情與來源**：NativeFormGrid 預設標籤 72 DIP、欄距 16 DIP、列距 14 DIP；詳情 Pivot 最小寬 460 DIP、最高 440 DIP。來源更新使用 80 DIP 標籤、20 DIP 列距與最小 460 DIP 內容。
- **擴充功能**：沿用既有最大 800 DIP 的商店／已安裝 Pivot、其他來源安裝 Expander；本次未調整其版面。

**The Content Measurement Rule.** 確認與進度窗依可見內容量測 client height，隱藏的欄位不預留間距；進階設定與來源詳情以 Flyout 開啟。

## Elevation & Depth

主窗、設定窗與下載窗使用 MicaBackdrop、DWM 圓角與系統框線，內容透明；底列用細線分隔。清單列沒有自訂陰影。ContentDialog、Flyout、MenuFlyout 的陰影、遮罩、焦點與動作狀態由原生範本處理；沒有自訂 shadow、動畫時長或 easing token。

## Shapes

基本控制項沿用 ControlCornerRadius，浮出層沿用 OverlayCornerRadius；類型標籤引用 download-kind。保留 TextBox、Button、ComboBox、NumberBox、PasswordBox、CheckBox、Pivot 與 ContentDialog 的原生形狀。下載列是平面清單，不用卡片或內嵌 Border 模擬視窗外框。

## Components

- **下載列與命令**：每列透明圖示按鈕依序是狀態主動作、資料夾、移除，提供 Tooltip 與 AutomationProperties.Name。主動作依狀態為暫停／繼續／重試／開啟；完成列雙擊開啟，其餘顯示詳情。清除已完成只清紀錄，排除解壓及做種中的任務；移除對話框提供另外刪檔的勾選，預設取消。
- **新增與確認**：主清單新增及瀏覽器接管使用同一個 DownloadWindow／DownloadForm。手動新增保留多行、Torrent、最近連結、分類及多檔選取；瀏覽器來源單行並隱藏手動入口。檔名與位置可編輯；單一來源先解析大小，再由使用者提交，多行來源批次建立。「直接開始下載」在表單可見，略過解析後仍須按開始。確認前不建立任務，取消關閉。
- **進階下載設定**：同一個底列箭頭打開寬 440 DIP、最高 480 DIP 的捲動 Flyout。連線數與 HTTP 標頭在上，DownloadOptionsPanel 以 HTTP、BitTorrent、代理、解壓縮四區呈現；自訂代理才顯示認證欄位，成對欄位等寬並排。保留方法／內容／憑證、Tracker、Torrent 自動下載、解壓密碼與刪除偏好。忙碌時停用開始、大小旁顯示 16 DIP ProgressRing；InfoBar 保留輸入。AddDownloadDialog 共用表單，但主清單入口使用獨立下載窗。
- **下載進度**：以類型標籤、檔名、位置、單行數據、進度條及狀態呈現。下載中主動作為一般按鈕；完成開啟及失敗重試使用系統重點色。完成後隱藏速度與剩餘時間，保留滿進度條及完成狀態；底列依序為資料夾、可用的做種停止、主動作與關閉。解壓時主動作停用；原檔已刪除時開啟解壓資料夾。未知大小以狀態文字回饋，做種及解壓仍輪詢，只有全部處理完成時停止。
- **進度的更多選項**：寬 440 DIP、最高 300 DIP 的 Flyout 保留可選取來源、複製網址及完成時的關閉偏好。按開啟成功後才依偏好關窗，不是完成後自動關閉。完成且不在解壓時，檔名可拖出對應檔案／資料夾。關閉只停止該窗輪詢，背景核心繼續下載。
- **設定六分頁**：下載、介面與行為、分類、連線、自動化、關於；各頁以一般區段標題與欄位成組，不再將全部設定塞入 Expander。涵蓋 HTTP／BT／eD2k、分類、壓縮檔與 Torrent、代理與鏡像、官方瀏覽器接管、遠端 HTTP／Token、Webhook、完成執行程式、主題與更新。單一儲存保存設定並關閉視窗，錯誤保留輸入；連接埠變更顯示重啟提示。標題與訊息共用頁首；NativeInfoBars 在關閉時收起訊息，避免空白間距。
- **詳情與分享**：TaskDetailsDialog 的資訊／檔案／連線 Pivot 使用 NativeFormGrid、可選取文字與原生 ListView。檔案名稱及大小分欄，選取後提供資料夾、分享、開啟；開啟及分享需完成且檔案存在。連線頁顯示 HTTP 狀態或 BT 統計。Windows 分享保留功能，未重做或實測目標流程。
- **修改來源**：DownloadSourceDialog 對齊網址與 HTTP 標頭，預設更新後繼續。也可等待下一個瀏覽器連結更新原任務；InfoBar 告知等待對象與取消入口。更新保留原任務 ID，與另建下載的「重新下載」分開。
- **擴充功能**：原有商店搜尋、排序、載入更多，以及已安裝的啟用、設定、更新、解除安裝保持原實作；本次 scope 不含版面改寫。
- **輸入與偏好**：剪貼簿／拖放文字接受 HTTP、HTTPS、magnet、eD2k、file URI，檔案拖放接受 Torrent。UiPreferences 保存最近最多 30 筆連結、下載位置與完成開啟後關窗偏好；外部明確位置優先。create protocol／官方 Native Messaging 先開獨立確認窗；extension 路由仍開主窗。
- **鍵盤與回饋**：Ctrl+N 新增、Ctrl+F 搜尋、F5 更新；清單焦點下 Ctrl+A 全選、Delete 移除；確認窗 Escape 取消、Ctrl+Enter 檢查／開始。保留原生鍵盤焦點、InfoBar 與 AutomationId，尚未宣稱 Narrator、高對比或跨環境視覺通過。

圖像沿用 `scripts/create_icon.py` 的 Pillow 下載箭頭及官方 WinUI 範本 SplashScreen／WideLogo；Mica noise 屬 SDK runtime。這次沒有新增 raster 或 AI 圖像；素材來源保留在 sidecar。

## Do's and Don'ts

### Do:

- Do 以 Downlism 的原生程式碼為參考，沿用 WinUI 控制項、ThemeResource 與完整互動狀態。
- Do 以繁體中文表達狀態、錯誤與操作，保持單列欄位及標籤對齊。
- Do 將設定放在獨立分頁視窗，下載進階選項與來源資訊按需開啟。
- Do 依可見內容量測下載視窗，隱藏可選區域時一起移除間距。
- Do 清楚區分原始碼／編譯檢查、歷史操作證據與本次原生視覺證據。

### Don't:

- Don't 以硬編碼顏色取代系統重點色、深淺主題及原生狀態。
- Don't 恢復大型主頁首、三列下載項目、常駐詳情欄或固定大進度窗。
- Don't 加入裝飾性卡片、儀表板摘要、Flutter／WebView 或 Downlism 網站展示版面。
- Don't 將舊版或空白截圖、UIA、尺寸量測及 publish 成功當作本次視覺驗收。
- Don't 宣稱尚未實測的 Narrator、高對比、Windows 10、RAM 節省或跨應用程式拖放。
