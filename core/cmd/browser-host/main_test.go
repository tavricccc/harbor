package main

import (
	"encoding/base64"
	"encoding/json"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"testing"
)

func TestOfficialExtensionContract(t *testing.T) {
	original := dataDir
	originalGrant := grantForeground
	dataDir = t.TempDir()
	grants := 0
	grantForeground = func(pid int) {
		if pid != 12345 {
			t.Errorf("foreground permission sent to wrong engine: %d", pid)
		}
		grants++
	}
	defer func() { dataDir = original; grantForeground = originalGrant }()
	created := false
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("X-Api-Token") != "fixture-token" {
			t.Error("missing authentication")
		}
		if r.Header.Get("X-Gopeed-Native-Confirmed") != "" {
			t.Error("browser must pass through native confirmation")
		}
		switch r.URL.Path {
		case "/api/v1/tasks":
			if grants == 0 {
				t.Error("download handoff preceded foreground permission")
			}
			var payload struct {
				Req struct {
					URL   string
					Extra struct{ Header map[string]string }
				}
			}
			if err := json.NewDecoder(r.Body).Decode(&payload); err != nil {
				t.Error(err)
			}
			if payload.Req.URL != "https://example.com/中文.zip" || payload.Req.Extra.Header["Sec-Ch-Ua"] != `"Chromium";v="130"` {
				t.Error("request changed")
			}
			created = true
		case "/api/v1/tasks/pause":
			if len(r.URL.Query()["id"]) != 2 {
				t.Error("array queries must remain repeated parameters")
			}
		}
		w.Header().Set("Content-Type", "application/json")
		w.Write([]byte(`{"code":0,"data":[]}`))
	}))
	defer server.Close()
	state, _ := json.Marshal(session{Port: server.Listener.Addr().(*net.TCPAddr).Port, Token: "fixture-token", PID: 12345})
	if err := os.WriteFile(filepath.Join(dataDir, "session.json"), state, 0600); err != nil {
		t.Fatal(err)
	}
	if value, err := handle(message{Method: "ping"}); err != nil || value != true {
		t.Fatalf("ping %v %v", value, err)
	}
	payload := []byte(`{"req":{"url":"https://example.com/中文.zip","extra":{"header":{"Sec-Ch-Ua":"\"Chromium\";v=\"130\""}}}}`)
	encoded, _ := json.Marshal(base64.StdEncoding.EncodeToString(payload))
	if _, err := handle(message{Method: "create", Params: encoded}); err != nil || !created {
		t.Fatalf("official base64 create: %v", err)
	}
	if _, err := handle(message{Method: "create", Params: json.RawMessage(`"!"`)}); err == nil {
		t.Fatal("malformed base64 accepted")
	}
	if _, err := handle(message{Method: "forward", Params: json.RawMessage(`{"path":"/api/v1/tasks/pause","method":"PUT","query":{"id":["first","second"]}}`)}); err != nil {
		t.Fatal(err)
	}
	if grants != 1 {
		t.Fatal("non-creation requests must not change foreground permission")
	}
	forward, _ := json.Marshal(map[string]any{"path": "/api/v1/tasks", "method": "POST", "query": map[string]string{"source": "browser"}, "data": json.RawMessage(payload)})
	if _, err := handle(message{Method: "forward", Params: forward}); err != nil || grants != 2 {
		t.Fatalf("forwarded task with query did not relay foreground permission: %v", err)
	}
	if _, err := handle(message{Method: "forward", Params: json.RawMessage(`{"path":"https://example.com"}`)}); err == nil {
		t.Fatal("external forwarding accepted")
	}
}
