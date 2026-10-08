# 安裝程式語系來源

`ChineseSimplified.isl` 與 `ChineseTraditional.isl` 取自 [Inno Setup 官方語系](https://jrsoftware.org/files/istrans/)，對應 `jrsoftware/issrc` commit `6d9c7612f31250fc46e335dd24fa4014be146fbd`。保留檔案內的譯者資料，授權附於 `LICENSE.txt`。

將語系檔放在專案內，讓本機與 CI 使用同一份內容；英文使用 Inno Setup 編譯器附帶的 `Default.isl`。Harbor 自訂的捷徑、啟動與錯誤訊息放在 `scripts/installer.iss` 的 `[CustomMessages]`。
