package main

import (
	"errors"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/GopeedLab/gopeed/pkg/base"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

func TestDeferredPersistScheduleAndRetry(t *testing.T) {
	root := t.TempDir()
	calls := 0
	fail := false
	create := func(request model.CreateTask) (string, error) {
		calls++
		if request.Req.Labels["harborDeferredId"] == "" {
			t.Fatal("missing recovery identifier")
		}
		if fail {
			return "", errors.New("source expired")
		}
		return "created", nil
	}
	queue, err := loadDeferredDownloads(root, create)
	if err != nil {
		t.Fatal(err)
	}
	req := func() model.CreateTask {
		return model.CreateTask{Req: &base.Request{URL: "https://example.com/file.zip"}, Opts: &base.Options{Path: root}}
	}
	later, err := queue.add([]model.CreateTask{req()}, nil)
	if err != nil {
		t.Fatal(err)
	}
	due := time.Now().Add(time.Minute)
	scheduled, err := queue.add([]model.CreateTask{req()}, &due)
	if err != nil {
		t.Fatal(err)
	}
	if calls != 0 {
		t.Fatal("saving a deferred request started a download")
	}
	queue, err = loadDeferredDownloads(root, create)
	if err != nil {
		t.Fatal(err)
	}
	queue.runDue(due.Add(-time.Second))
	if calls != 0 {
		t.Fatal("schedule started early")
	}
	fail = true
	queue.runDue(due)
	if calls != 1 || queue.entries[1].Error != "source expired" {
		t.Fatal("failed schedule did not remain visible")
	}
	queue.runDue(due.Add(time.Hour))
	if calls != 1 {
		t.Fatal("failed schedule retried automatically")
	}
	fail = false
	if id, err := queue.start(1); err != nil || id != "created" {
		t.Fatalf("manual retry: %s %v", id, err)
	}
	if len(queue.entries) != 1 || queue.entries[0].ID != later[0] {
		t.Fatal("manual request was not retained")
	}
	queue, err = loadDeferredDownloads(root, create)
	if err != nil {
		t.Fatal(err)
	}
	if len(queue.entries) != 1 || queue.entries[0].ID == scheduled[0] {
		t.Fatal("completed handoff reappeared after restart")
	}
}

func TestDeferredAPIRequiresAuthAndDoesNotFetch(t *testing.T) {
	queue, err := loadDeferredDownloads(t.TempDir(), func(model.CreateTask) (string, error) { t.Fatal("save fetched source"); return "", nil })
	if err != nil {
		t.Fatal(err)
	}
	handler := queue.handler(http.NotFoundHandler(), "secret")
	body := `{"reqs":[{"req":{"url":"magnet:?xt=urn:btih:123&dn=Archive"}}]}`
	for _, authorized := range []bool{false, true} {
		request := httptest.NewRequest("POST", "/api/v1/native/queue", strings.NewReader(body))
		if authorized {
			request.Header.Set("X-Api-Token", "secret")
		}
		response := httptest.NewRecorder()
		handler.ServeHTTP(response, request)
		if !authorized && response.Code != 401 {
			t.Fatal("unauthorized queue write accepted")
		}
		if authorized && !strings.Contains(response.Body.String(), `"code":0`) {
			t.Fatal(response.Body.String())
		}
	}
	if len(queue.entries) != 1 || queue.taskList()[0]["name"] != "Archive" {
		t.Fatal("magnet presentation mismatch")
	}
}
