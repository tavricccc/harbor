package main

import (
	"os"
	"os/exec"
	"sync"
	"syscall"

	"harbor/localization"

	"github.com/getlantern/systray"
)

var trayLanguageMutex sync.Mutex
var trayOpen, trayQuit *systray.MenuItem

func refreshTrayLanguage() {
	trayLanguageMutex.Lock()
	defer trayLanguageMutex.Unlock()
	if trayOpen == nil {
		return
	}
	systray.SetTooltip(localization.Text("Tray.Tooltip"))
	trayOpen.SetTitle(localization.Text("Tray.Open"))
	trayOpen.SetTooltip(localization.Text("Tray.OpenHint"))
	trayQuit.SetTitle(localization.Text("Tray.Quit"))
	trayQuit.SetTooltip(localization.Text("Tray.QuitHint"))
}

func runTray(ui, iconPath string, stop chan os.Signal) {
	systray.Run(func() {
		icon, err := os.ReadFile(iconPath)
		if err == nil {
			systray.SetIcon(icon)
		}
		trayLanguageMutex.Lock()
		open := systray.AddMenuItem(localization.Text("Tray.Open"), localization.Text("Tray.OpenHint"))
		systray.AddSeparator()
		quit := systray.AddMenuItem(localization.Text("Tray.Quit"), localization.Text("Tray.QuitHint"))
		trayOpen, trayQuit = open, quit
		trayLanguageMutex.Unlock()
		refreshTrayLanguage()
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
