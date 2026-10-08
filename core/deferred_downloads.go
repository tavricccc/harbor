package main

import (
	"encoding/json"
	"fmt"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

type deferredDownload struct {
	ID        string           `json:"id"`
	CreatedAt time.Time        `json:"createdAt"`
	StartAt   *time.Time       `json:"startAt,omitempty"`
	Request   model.CreateTask `json:"request"`
	Error     string           `json:"error,omitempty"`
}

type deferredDownloads struct {
	mu      sync.Mutex
	path    string
	entries []deferredDownload
	create  func(model.CreateTask) (string, error)
}

func loadDeferredDownloads(root string, create func(model.CreateTask) (string, error)) (*deferredDownloads, error) {
	q := &deferredDownloads{path: filepath.Join(root, "deferred-downloads.json"), entries: []deferredDownload{}, create: create}
	data, err := os.ReadFile(q.path)
	if os.IsNotExist(err) {
		return q, nil
	}
	if err != nil {
		return nil, err
	}
	if err = json.Unmarshal(data, &q.entries); err != nil {
		return nil, err
	}
	return q, nil
}

func (q *deferredDownloads) save() error {
	data, err := json.Marshal(q.entries)
	if err != nil {
		return err
	}
	if err = os.WriteFile(q.path+".tmp", data, 0600); err != nil {
		return err
	}
	return os.Rename(q.path+".tmp", q.path)
}

func (q *deferredDownloads) add(requests []model.CreateTask, startAt *time.Time) ([]string, error) {
	q.mu.Lock()
	defer q.mu.Unlock()
	before := len(q.entries)
	ids := []string{}
	for _, request := range requests {
		id := "deferred-" + newDeferredID()
		if request.Req.Labels == nil {
			request.Req.Labels = map[string]string{}
		}
		request.Req.Labels["harborDeferredId"] = id
		q.entries = append(q.entries, deferredDownload{ID: id, CreatedAt: time.Now().UTC(), StartAt: startAt, Request: request})
		ids = append(ids, id)
	}
	if err := q.save(); err != nil {
		q.entries = q.entries[:before]
		return nil, err
	}
	return ids, nil
}

func (q *deferredDownloads) remove(index int) error {
	before := append([]deferredDownload(nil), q.entries...)
	q.entries = append(q.entries[:index], q.entries[index+1:]...)
	if err := q.save(); err != nil {
		q.entries = before
		return err
	}
	return nil
}

func (q *deferredDownloads) start(index int) (string, error) {
	id, err := q.create(q.entries[index].Request)
	if err != nil {
		return "", err
	}
	return id, q.remove(index)
}

func (q *deferredDownloads) runDue(now time.Time) {
	q.mu.Lock()
	defer q.mu.Unlock()
	for index := 0; index < len(q.entries); {
		entry := &q.entries[index]
		if entry.StartAt == nil || entry.StartAt.After(now) || entry.Error != "" {
			index++
			continue
		}
		if _, err := q.start(index); err != nil {
			// Failed starts stay visible for explicit retry, rather than looping forever.
			q.entries[index].Error = err.Error()
			if saveErr := q.save(); saveErr != nil {
				rest.Downloader.Logger.Error().Err(saveErr).Msg("save scheduled download failure")
			}
			index++
		}
	}
}

func (q *deferredDownloads) taskList() []map[string]any {
	tasks := []map[string]any{}
	for _, entry := range q.entries {
		name := entry.Request.Req.URL
		if parsed, err := url.Parse(name); err == nil {
			if parsed.Scheme == "magnet" {
				if label := parsed.Query().Get("dn"); label != "" {
					name = label
				}
			} else {
				name = filepath.Base(parsed.Path)
			}
		}
		if entry.Request.Opts != nil && entry.Request.Opts.Name != "" {
			name = entry.Request.Opts.Name
		}
		tasks = append(tasks, map[string]any{"id": entry.ID, "name": name, "status": "deferred", "createdAt": entry.CreatedAt,
			"scheduledAt": entry.StartAt, "error": entry.Error, "meta": map[string]any{"req": entry.Request.Req, "opts": entry.Request.Opts}})
	}
	return tasks
}

func (q *deferredDownloads) handler(next http.Handler, token string) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/api/v1/native/queue" && !strings.HasPrefix(r.URL.Path, "/api/v1/native/queue/") {
			next.ServeHTTP(w, r)
			return
		}
		if r.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		if r.URL.Path == "/api/v1/native/queue" && r.Method == http.MethodPost {
			var input struct {
				Requests []model.CreateTask `json:"reqs"`
				StartAt  *time.Time         `json:"startAt"`
			}
			if err := json.NewDecoder(http.MaxBytesReader(w, r.Body, 1024*1024)).Decode(&input); err != nil || len(input.Requests) == 0 {
				rest.WriteJson(w, model.NewErrorResult("download link is required"))
				return
			}
			for _, request := range input.Requests {
				if request.Req == nil || request.Req.Validate() != nil {
					rest.WriteJson(w, model.NewErrorResult("invalid download link"))
					return
				}
			}
			if input.StartAt != nil && !input.StartAt.After(time.Now()) {
				rest.WriteJson(w, model.NewErrorResult("schedule must be in the future"))
				return
			}
			ids, err := q.add(input.Requests, input.StartAt)
			if err != nil {
				rest.WriteJson(w, model.NewErrorResult(err.Error()))
				return
			}
			rest.WriteJson(w, model.NewOkResult(ids))
			return
		}
		q.mu.Lock()
		defer q.mu.Unlock()
		if r.URL.Path == "/api/v1/native/queue" && r.Method == http.MethodGet {
			rest.WriteJson(w, model.NewOkResult(q.taskList()))
			return
		}
		suffix := strings.TrimPrefix(r.URL.Path, "/api/v1/native/queue/")
		id := strings.TrimSuffix(suffix, "/start")
		for index, entry := range q.entries {
			if entry.ID != id {
				continue
			}
			var result string
			var err error
			switch {
			case r.Method == http.MethodDelete && suffix == id:
				err = q.remove(index)
			case r.Method == http.MethodPatch && suffix == id:
				var input struct {
					StartAt *time.Time `json:"startAt"`
				}
				if decodeErr := json.NewDecoder(http.MaxBytesReader(w, r.Body, 1024)).Decode(&input); decodeErr != nil {
					rest.WriteJson(w, model.NewErrorResult("invalid schedule data"))
					return
				}
				if input.StartAt != nil && !input.StartAt.After(time.Now()) {
					rest.WriteJson(w, model.NewErrorResult("schedule must be in the future"))
					return
				}
				before := q.entries[index]
				q.entries[index].StartAt = input.StartAt
				q.entries[index].Error = ""
				err = q.save()
				if err != nil {
					q.entries[index] = before
				}
			case r.Method == http.MethodPut && suffix == id+"/start":
				result, err = q.start(index)
			default:
				http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
				return
			}
			if err != nil {
				rest.WriteJson(w, model.NewErrorResult(err.Error()))
				return
			}
			rest.WriteJson(w, model.NewOkResult(result))
			return
		}
		rest.WriteJson(w, model.NewErrorResult(fmt.Sprintf("pending download not found: %s", id), model.CodeTaskNotFound))
	})
}
