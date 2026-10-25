package main

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/GopeedLab/gopeed/pkg/download"
)

func TestTaskRevisionIgnoresProgress(t *testing.T) {
	events := &taskEvents{}
	revision := trackRevision(events)
	handler := revision.handler(http.NotFoundHandler(), "secret")
	read := func() uint64 {
		t.Helper()
		request := httptest.NewRequest(http.MethodGet, "/api/v1/native/revision", nil)
		request.Header.Set("X-Api-Token", "secret")
		response := httptest.NewRecorder()
		handler.ServeHTTP(response, request)
		var result struct {
			Data uint64 `json:"data"`
		}
		if err := json.Unmarshal(response.Body.Bytes(), &result); err != nil {
			t.Fatal(err)
		}
		return result.Data
	}
	task := &download.Task{ID: "task"}
	events.emit(&download.Event{Key: download.EventKeyProgress, Task: task})
	if read() != 0 {
		t.Fatal("progress ticks must not force a full task snapshot")
	}
	events.emit(&download.Event{Key: download.EventKeyDone, Task: task})
	if read() != 1 {
		t.Fatal("a finished download must move the revision")
	}
	unauthorized := httptest.NewRecorder()
	handler.ServeHTTP(unauthorized, httptest.NewRequest(http.MethodGet, "/api/v1/native/revision", nil))
	if unauthorized.Code != http.StatusUnauthorized {
		t.Fatalf("revision without token returned %d", unauthorized.Code)
	}
}
