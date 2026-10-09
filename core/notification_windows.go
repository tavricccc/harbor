package main

import (
	"golang.org/x/sys/windows"
	"harbor/localization"
	"os"
	"unicode/utf16"
	"unsafe"
)

// Use the existing systray notification icon owned by this process.
// NOTIFYICONDATAW layout: https://learn.microsoft.com/windows/win32/api/shellapi/ns-shellapi-notifyicondataw
type balloonData struct {
	Size                uint32
	Window              uintptr
	ID, Flags, Callback uint32
	Icon                uintptr
	Tip                 [128]uint16
	State, StateMask    uint32
	Info                [256]uint16
	Timeout             uint32
	Title               [64]uint16
	InfoFlags           uint32
	Guid                windows.GUID
	BalloonIcon         uintptr
}

func notifyComplete(name string) {
	user := windows.NewLazySystemDLL("user32.dll")
	find := user.NewProc("FindWindowExW")
	process := user.NewProc("GetWindowThreadProcessId")
	class, _ := windows.UTF16PtrFromString("SystrayClass")
	var window uintptr
	for {
		next, _, _ := find.Call(0, window, uintptr(unsafe.Pointer(class)), 0)
		if next == 0 {
			return
		}
		window = next
		var pid uint32
		process.Call(window, uintptr(unsafe.Pointer(&pid)))
		if pid == uint32(os.Getpid()) {
			break
		}
	}
	data := balloonData{Window: window, ID: 100, Flags: 0x10, InfoFlags: 1}
	data.Size = uint32(unsafe.Sizeof(data))
	copy(data.Title[:len(data.Title)-1], utf16.Encode([]rune(localization.Text("Notification.Completed"))))
	copy(data.Info[:len(data.Info)-1], utf16.Encode([]rune(name)))
	windows.NewLazySystemDLL("shell32.dll").NewProc("Shell_NotifyIconW").Call(1, uintptr(unsafe.Pointer(&data)))
}
