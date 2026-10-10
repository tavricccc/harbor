package foreground

import "golang.org/x/sys/windows"

var allowSetForegroundWindow = windows.NewLazySystemDLL("user32.dll").NewProc("AllowSetForegroundWindow")

// Allow passes the caller's foreground permission to the next process in the
// browser -> native host -> engine -> frontend activation chain.
func Allow(processID int) {
	if processID > 0 {
		allowSetForegroundWindow.Call(uintptr(processID))
	}
}
