package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"testing"
	"time"

	"github.com/GopeedLab/gopeed/pkg/base"
	"github.com/GopeedLab/gopeed/pkg/download"
	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

type pacedWriter struct{ http.ResponseWriter }

func (w pacedWriter) Write(p []byte) (int, error) {
	time.Sleep(8 * time.Millisecond)
	return w.ResponseWriter.Write(p)
}

// One integration test protects the API contract, pause/resume, disk persistence,
// and final bytes. It uses a local server, not an unreliable public endpoint.
func TestDownloadPauseRestartResume(t *testing.T) {
	payload := bytes.Repeat([]byte("Harbor download verification\n"), 200000)
	fixture := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.ServeContent(pacedWriter{w}, r, "fixture.bin", time.Unix(0, 0), bytes.NewReader(payload))
	}))
	defer fixture.Close()
	root := t.TempDir()
	destination := filepath.Join(root, "downloads")
	os.MkdirAll(destination, 0700)
	config := &model.StartConfig{Address: "127.0.0.1:0", Storage: model.StorageBolt, StorageDir: root, ApiToken: "test-secret", ProductionMode: false, RefreshInterval: 50}
	port, err := rest.Start(config)
	if err != nil {
		t.Fatal(err)
	}
	defer rest.Stop()
	lifecycle := trackLifecycle(rest.Downloader)
	endpoint := fmt.Sprintf("http://127.0.0.1:%d/api/v1/", port)
	call := func(method, route string, body any) json.RawMessage {
		t.Helper()
		data, _ := json.Marshal(body)
		req, _ := http.NewRequest(method, endpoint+route, bytes.NewReader(data))
		req.Header.Set("X-Api-Token", "test-secret")
		req.Header.Set("Content-Type", "application/json")
		response, e := http.DefaultClient.Do(req)
		if e != nil {
			t.Fatal(e)
		}
		defer response.Body.Close()
		var result struct {
			Code int             `json:"code"`
			Msg  string          `json:"msg"`
			Data json.RawMessage `json:"data"`
		}
		if e = json.NewDecoder(response.Body).Decode(&result); e != nil {
			t.Fatal(e)
		}
		if result.Code != 0 {
			t.Fatal(result.Msg)
		}
		return result.Data
	}
	unauthorized, _ := http.Get(endpoint + "tasks")
	io.Copy(io.Discard, unauthorized.Body)
	unauthorized.Body.Close()
	if unauthorized.StatusCode != 401 {
		t.Fatalf("unauthenticated API returned %d", unauthorized.StatusCode)
	}
	created := call("POST", "tasks", map[string]any{"req": map[string]any{"url": fixture.URL + "/fixture.bin"}, "opts": map[string]any{"path": destination, "name": "verified.bin", "extra": map[string]any{"connections": 2}}})
	var id string
	json.Unmarshal(created, &id)
	task := func() *download.Task {
		var task download.Task
		json.Unmarshal(call("GET", "tasks/"+id, nil), &task)
		return &task
	}
	wait := func(condition func(*download.Task) bool) {
		t.Helper()
		deadline := time.Now().Add(20 * time.Second)
		for time.Now().Before(deadline) {
			current := task()
			if current.Status == base.DownloadStatusError {
				t.Fatalf("download error: %+v", current)
			}
			if condition(current) {
				return
			}
			time.Sleep(30 * time.Millisecond)
		}
		t.Fatal("download state timed out")
	}
	wait(func(task *download.Task) bool { return task.Progress != nil && task.Progress.Downloaded > 10000 })
	call("PUT", "tasks/"+id+"/pause", nil)
	if task().Status != base.DownloadStatusPause {
		t.Fatal("pause did not persist")
	}
	if err := lifecycle.pauseAndWait(); err != nil {
		t.Fatal(err)
	}
	rest.Stop()
	port, err = rest.Start(config)
	if err != nil {
		t.Fatal(err)
	}
	endpoint = fmt.Sprintf("http://127.0.0.1:%d/api/v1/", port)
	if task().ID != id {
		t.Fatal("task missing after restart")
	}
	call("PUT", "tasks/"+id+"/continue", nil)
	wait(func(task *download.Task) bool { return task.Status == base.DownloadStatusDone })
	actual, err := os.ReadFile(filepath.Join(destination, "verified.bin"))
	if err != nil {
		t.Fatal(err)
	}
	if sha256.Sum256(actual) != sha256.Sum256(payload) {
		t.Fatal("resumed file checksum mismatch")
	}
	call("DELETE", "tasks/"+id+"?force=false", nil)
	if _, err = os.Stat(filepath.Join(destination, "verified.bin")); err != nil {
		t.Fatal("remove task unexpectedly deleted file")
	}
}
