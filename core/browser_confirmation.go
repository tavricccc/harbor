package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"

	"harbor/core/internal/foreground"

	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

// The official extension's remote mode creates tasks immediately. Keep that
// endpoint compatible, but hand external creations to a native confirmation.
func browserConfirmation(next http.Handler, token string, handoff func([]byte) (string, error)) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.URL.Path != "/api/v1/tasks" || r.Header.Get("X-Gopeed-Native-Confirmed") == "1" {
			next.ServeHTTP(w, r)
			return
		}
		if r.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		body, err := io.ReadAll(http.MaxBytesReader(w, r.Body, 1024*1024))
		var task model.CreateTask
		if err != nil || json.Unmarshal(body, &task) != nil || task.Req == nil || task.Req.URL == "" {
			http.Error(w, "invalid download request", http.StatusBadRequest)
			return
		}
		id, err := handoff(body)
		if err != nil {
			http.Error(w, "could not open download confirmation", http.StatusInternalServerError)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(map[string]any{"code": 0, "data": id})
	})
}

func openDownloadRequest(root, ui string, body []byte) (string, error) {
	directory := filepath.Join(root, "pending-downloads")
	if err := os.MkdirAll(directory, 0700); err != nil {
		return "", err
	}
	key := make([]byte, 16)
	if _, err := rand.Read(key); err != nil {
		return "", err
	}
	id := hex.EncodeToString(key)
	path := filepath.Join(directory, id+".json")
	if err := os.WriteFile(path, body, 0600); err != nil {
		return "", err
	}
	command := exec.Command(ui, "--download-request", id)
	if err := command.Start(); err != nil {
		os.Remove(path)
		return "", err
	}
	foreground.Allow(command.Process.Pid)
	go command.Wait()
	return id, nil
}
