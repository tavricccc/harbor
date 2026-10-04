package main

import (
	"os"
	"os/exec"
	"syscall"

	"github.com/getlantern/systray"
)

func runTray(ui, iconPath string, stop chan os.Signal) {
	systray.Run(func() {
		icon, err := os.ReadFile(iconPath)
		if err == nil {
			systray.SetIcon(icon)
		}
		systray.SetTooltip("Harbor — 背景下載")
		open := systray.AddMenuItem("開啟 Harbor", "檢視下載佇列")
		systray.AddSeparator()
		quit := systray.AddMenuItem("停止下載並結束", "保存進度並停止核心")
		go func() {
			for {
				select {
				case <-open.ClickedCh:
					command := exec.Command(ui)
					command.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
					command.Start()
				case <-quit.ClickedCh:
					systray.Quit()
					return
				case <-stop:
					systray.Quit()
					return
				}
			}
		}()
	}, func() {})
}
