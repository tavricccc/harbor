package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"flag"
	"fmt"
	"net"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"time"

	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

type session struct {
	Port        int    `json:"port"`
	Token       string `json:"token"`
	PID         int    `json:"pid"`
	ControlPort int    `json:"controlPort"`
}

func main() {
	root := flag.String("data", "", "Application data directory")
	ui := flag.String("ui", "", "Native frontend executable")
	icon := flag.String("icon", "", "Tray icon path")
	apiPort := flag.Int("port", 18762, "Local API port")
	shutdown := flag.Bool("shutdown", false, "Gracefully stop the running core")
	flag.Parse()
	if *root == "" {
		panic("--data is required")
	}
	if *shutdown {
		payload, err := os.ReadFile(filepath.Join(*root, "session.json"))
		if os.IsNotExist(err) {
			return
		}
		if err != nil {
			panic(err)
		}
		var current session
		if err := json.Unmarshal(payload, &current); err != nil {
			panic(err)
		}
		request, _ := http.NewRequest("POST", fmt.Sprintf("http://127.0.0.1:%d/shutdown", current.ControlPort), nil)
		request.Header.Set("X-Api-Token", current.Token)
		client := http.Client{Timeout: 20 * time.Second}
		response, err := client.Do(request)
		if err != nil {
			return
		}
		response.Body.Close()
		deadline := time.Now().Add(20 * time.Second)
		for time.Now().Before(deadline) {
			if _, err := os.Stat(filepath.Join(*root, "session.json")); os.IsNotExist(err) {
				return
			}
			time.Sleep(100 * time.Millisecond)
		}
		return
	}
	if err := os.MkdirAll(*root, 0700); err != nil {
		panic(err)
	}
	release, acquired := acquireCore(*root)
	if !acquired {
		return
	}
	defer release()
	tokenPath := filepath.Join(*root, "api-token")
	tokenBytes, err := os.ReadFile(tokenPath)
	if os.IsNotExist(err) {
		secret := make([]byte, 32)
		if _, err := rand.Read(secret); err != nil {
			panic(err)
		}
		tokenBytes = []byte(hex.EncodeToString(secret))
		if err := os.WriteFile(tokenPath, tokenBytes, 0600); err != nil {
			panic(err)
		}
	} else if err != nil {
		panic(err)
	}
	token := string(tokenBytes)
	api, apiListener, err := rest.BuildServer(&model.StartConfig{
		Address: fmt.Sprintf("127.0.0.1:%d", *apiPort), Storage: model.StorageBolt,
		StorageDir: *root, ApiToken: token, ProductionMode: true,
		RefreshInterval: 1000,
	})
	if err != nil {
		panic(err)
	}
	port := apiListener.Addr().(*net.TCPAddr).Port
	api.Handler = browserConfirmation(api.Handler, token, func(body []byte) (string, error) {
		return openDownloadRequest(*root, *ui, body)
	})
	api.Handler = localExtensions(api.Handler, token)
	queue, err := loadDeferredDownloads(*root, func(request model.CreateTask) (string, error) {
		// Recover a handoff interrupted after the engine saved the task but before
		// the pending entry was removed, without creating a second download.
		if id := request.Req.Labels["harborDeferredId"]; id != "" {
			for _, task := range rest.Downloader.GetTasks() {
				if task.Meta.Req.Labels["harborDeferredId"] == id {
					return task.ID, nil
				}
			}
		}
		return rest.Downloader.CreateDirect(request.Req, request.Opts)
	})
	if err != nil {
		panic(err)
	}
	api.Handler = queue.handler(api.Handler, token)
	queueStop := make(chan struct{})
	queueDone := make(chan struct{})
	go func() {
		defer close(queueDone)
		ticker := time.NewTicker(time.Second)
		defer ticker.Stop()
		for {
			select {
			case now := <-ticker.C:
				queue.runDue(now)
			case <-queueStop:
				return
			}
		}
	}()
	go api.Serve(apiListener)
	lifecycle := trackLifecycle(rest.Downloader)
	config, err := rest.Downloader.GetConfig()
	if err != nil {
		panic(err)
	}
	if config.DownloadDir == "" {
		home, err := os.UserHomeDir()
		if err != nil {
			panic(err)
		}
		config.DownloadDir = filepath.Join(home, "Downloads")
		if err := rest.Downloader.PutConfig(config); err != nil {
			panic(err)
		}
	}
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt)
	// A separate authenticated lifecycle endpoint avoids modifications to upstream.
	control := http.NewServeMux()
	control.HandleFunc("POST /shutdown", func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", 401)
			return
		}
		w.WriteHeader(204)
		select {
		case stop <- os.Interrupt:
		default:
		}
	})
	listener, err := listenControl()
	if err != nil {
		panic(err)
	}
	server := &http.Server{Handler: control, ReadHeaderTimeout: 5 * time.Second}
	go server.Serve(listener)
	defer server.Close()
	state := session{port, token, os.Getpid(), listener.Addr().(*net.TCPAddr).Port}
	payload, _ := json.Marshal(state)
	sessionPath := filepath.Join(*root, "session.json")
	if err := os.WriteFile(sessionPath+".tmp", payload, 0600); err != nil {
		panic(err)
	}
	if err := os.Rename(sessionPath+".tmp", sessionPath); err != nil {
		panic(err)
	}
	startExtras(rest.Downloader, port, token)
	defer func() { api.Close(); rest.Stop(); os.Remove(sessionPath) }()
	fmt.Println("Gopeed Native core ready")
	if *ui == "" {
		<-stop
	} else {
		runTray(*ui, *icon, stop)
	}
	close(queueStop)
	<-queueDone
	if err := lifecycle.pauseAndWait(); err != nil {
		rest.Downloader.Logger.Error().Err(err).Msg("save before shutdown failed")
	}
}
