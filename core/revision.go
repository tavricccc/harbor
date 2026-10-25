package main

import (
	"net/http"
	"sync/atomic"

	"github.com/GopeedLab/gopeed/pkg/download"
	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

// Every lifecycle event (start, pause, done, error, delete) bumps a revision.
// The native list polls live progress only and takes a full task snapshot when
// the revision moves, so a download that finishes between ticks still appears.
type taskRevision struct {
	value atomic.Uint64
}

func trackRevision(events *taskEvents) *taskRevision {
	revision := &taskRevision{}
	events.add(func(event *download.Event) {
		if event.Key != download.EventKeyProgress {
			revision.value.Add(1)
		}
	})
	return revision
}

func (r *taskRevision) handler(next http.Handler, token string) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, req *http.Request) {
		if req.URL.Path != "/api/v1/native/revision" {
			next.ServeHTTP(w, req)
			return
		}
		if req.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		if req.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		rest.WriteJson(w, model.NewOkResult(r.value.Load()))
	})
}
