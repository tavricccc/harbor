package main

import (
	"flag"
	"os"
	"os/exec"
)

type coreOptions struct {
	root, ui, icon, language string
	apiPort                  int
	shutdown                 bool
}

func main() {
	// Torrent selects its storage backend during package initialization.
	if os.Getenv("TORRENT_STORAGE_DEFAULT_FILE_IO") != "classic" {
		if err := os.Setenv("TORRENT_STORAGE_DEFAULT_FILE_IO", "classic"); err != nil {
			panic(err)
		}
		command := exec.Command(os.Args[0], os.Args[1:]...)
		if err := command.Run(); err != nil {
			panic(err)
		}
		return
	}
	var options coreOptions
	flag.StringVar(&options.root, "data", "", "Application data directory")
	flag.StringVar(&options.ui, "ui", "", "Native frontend executable")
	flag.StringVar(&options.icon, "icon", "", "Tray icon path")
	flag.IntVar(&options.apiPort, "port", 18762, "Local API port")
	flag.StringVar(&options.language, "language", "en-US", "Interface language")
	flag.BoolVar(&options.shutdown, "shutdown", false, "Gracefully stop the running core")
	flag.Parse()
	if options.root == "" {
		panic("--data is required")
	}
	if options.shutdown {
		if err := shutdownCore(options.root); err != nil {
			panic(err)
		}
		return
	}
	if err := runCore(options); err != nil {
		panic(err)
	}
}
