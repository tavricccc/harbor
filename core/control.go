package main

import (
	"encoding/json"
	"fmt"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"time"

	"harbor/localization"
)

func shutdownCore(root string) error {
	path := filepath.Join(root, "session.json")
	payload, err := os.ReadFile(path)
	if os.IsNotExist(err) {
		return nil
	}
	if err != nil {
		return err
	}
	var current session
	if err := json.Unmarshal(payload, &current); err != nil {
		return err
	}
	request, err := http.NewRequest("POST", fmt.Sprintf("http://127.0.0.1:%d/shutdown", current.ControlPort), nil)
	if err != nil {
		return err
	}
	request.Header.Set("X-Api-Token", current.Token)
	client := http.Client{Timeout: 20 * time.Second}
	response, err := client.Do(request)
	if err != nil {
		return nil
	}
	response.Body.Close()
	deadline := time.Now().Add(20 * time.Second)
	for time.Now().Before(deadline) {
		if _, err := os.Stat(path); os.IsNotExist(err) {
			return nil
		}
		time.Sleep(100 * time.Millisecond)
	}
	return nil
}

func startControl(token string, stop chan os.Signal) (*http.Server, int, error) {
	control := http.NewServeMux()
	control.HandleFunc("POST /language", func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		var request struct {
			Language string `json:"language"`
		}
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			http.Error(w, "invalid language request", http.StatusBadRequest)
			return
		}
		if err := localization.Set(request.Language); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		refreshTrayLanguage()
		w.WriteHeader(http.StatusNoContent)
	})
	control.HandleFunc("POST /shutdown", func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		w.WriteHeader(http.StatusNoContent)
		select {
		case stop <- os.Interrupt:
		default:
		}
	})
	listener, err := listenControl()
	if err != nil {
		return nil, 0, err
	}
	server := &http.Server{Handler: control, ReadHeaderTimeout: 5 * time.Second}
	go server.Serve(listener)
	return server, listener.Addr().(*net.TCPAddr).Port, nil
}
