package main

import (
	"crypto/sha256"
	"fmt"
	"path/filepath"
	"strings"
	"syscall"
	"unsafe"
)

// One core owns each data directory, including simultaneous UI and browser starts.
func acquireCore(root string) (func(), bool) {
	absolute, err := filepath.Abs(root)
	if err != nil {
		panic(err)
	}
	name, err := syscall.UTF16PtrFromString(fmt.Sprintf("Local\\Harbor.%x", sha256.Sum256([]byte(strings.ToLower(absolute)))))
	if err != nil {
		panic(err)
	}
	kernel := syscall.NewLazyDLL("kernel32.dll")
	handle, _, last := kernel.NewProc("CreateMutexW").Call(0, 0, uintptr(unsafe.Pointer(name)))
	if handle == 0 {
		panic(last)
	}
	closeHandle := func() { kernel.NewProc("CloseHandle").Call(handle) }
	if last == syscall.Errno(183) {
		closeHandle()
		return nil, false
	}
	return closeHandle, true
}
