# 原生下載器介面方向

模式：Operate。平台：Windows desktop / WinUI 3。修訂：2026-10-03。

視覺權威是使用者指定的 `C:/Users/Tavric/projects/downlism/src/Downlism.App`，包括 `MainWindow.xaml`、`App.xaml`、`NewDownloadWindow.xaml/.cs`、`SettingsWindow.xaml/.cs`。以程式碼及現有有效截圖交叉確認比例；不採用 Downlism 網站樣式。擴充功能不在本次改版範圍，官方瀏覽器整合保留。

## Direction contract

THESIS：所有下載操作採 Downlism 的原生結構，下載清單、確認、進度、設定及詳情共用相同文字與間距；主清單每列保持平靜，主動作只在確認／進度底列強調。

OWN-WORLD：Windows Mica、系統標題列與原生控制項。內文 Segoe UI Variable Text／Microsoft JhengHei UI 14 DIP，輔助資訊 12 DIP。清單 44 DIP 最小列高、12 DIP 欄距、透明小圖示按鈕。底列細線，無裝飾性卡片。

STORY：貼上或新增連結、確認名稱與位置、開始下載；進度視窗呈現狀態及下一步。設定在獨立視窗用頂部分頁尋找，完整 Gopeed 協定與進階功能沿用相同表單節奏。

FIRST VIEWPORT：主視窗預設 1120×720 DIP，保留 DPI 可讀性；CommandBar margin 12,8,12,0，右側承載貼上／新增／全部暫停／全部繼續／設定。篩選列 margin 20,0、gap 10，清除已完成放末端；表格欄寬 *,140,152,104,100，狀態摘要在底部。

FORM：使用者固定指定 Downlism，方向識別 `user-pinned-downlism`；直接移植原生介面結構。下載內容寬 660 DIP，padding 24,20,24,16，label 52 DIP、gap 16；footer 24,14、button gap 8。進階項目按需浮出且可捲動，不壓縮主表單。

FINISH：unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance.

原生 XAML 不執行 HTML/CSS detector。保留既有素材，不生成新的裝飾圖像。新版視覺只能由新版有效原生畫面證實；歷史截圖、編譯及 source review 各自記錄其範圍。
