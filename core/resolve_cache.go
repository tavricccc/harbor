package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"reflect"
	"sync"
	"time"
	"unsafe"

	"github.com/GopeedLab/gopeed/pkg/download"
)

const resolveTTL = 10 * time.Minute

// Upstream keeps every resolved fetcher until a task consumes it. An abandoned
// probe keeps prefetching into gopeed-prefetch-* and holds the file open, so
// even shutdown cannot remove it. The cache is unexported and release builds
// use the official module, so reach it by reflection; tests catch renames.
type resolveCache struct {
	fetchers reflect.Value // *map[string]fetcher.Fetcher
	lock     *sync.RWMutex
	mutex    sync.Mutex
	seen     map[string]time.Time
	creating map[string]int
	stop     chan struct{}
	done     chan struct{}
}

func trackResolves(d *download.Downloader) (*resolveCache, error) {
	value := reflect.ValueOf(d).Elem()
	fetchers, lock := value.FieldByName("fetcherCache"), value.FieldByName("fetcherMapLock")
	if fetchers.Kind() != reflect.Map || fetchers.Type().Key().Kind() != reflect.String || lock.Type() != reflect.TypeOf((*sync.RWMutex)(nil)) {
		return nil, errors.New("unsupported Gopeed resolve cache")
	}
	if _, ok := fetchers.Type().Elem().MethodByName("Close"); !ok {
		return nil, errors.New("unsupported Gopeed fetcher")
	}
	return &resolveCache{
		fetchers: reflect.NewAt(fetchers.Type(), unsafe.Pointer(fetchers.UnsafeAddr())),
		lock:     *(**sync.RWMutex)(unsafe.Pointer(lock.UnsafeAddr())),
		seen:     map[string]time.Time{},
		creating: map[string]int{},
	}, nil
}

func (c *resolveCache) start(interval time.Duration) {
	c.stop, c.done = make(chan struct{}), make(chan struct{})
	go func() {
		defer close(c.done)
		ticker := time.NewTicker(interval)
		defer ticker.Stop()
		for {
			select {
			case now := <-ticker.C:
				c.expire(now, resolveTTL)
			case <-c.stop:
				return
			}
		}
	}()
}

// expire releases fetchers first seen more than ttl ago. A zero ttl releases all.
func (c *resolveCache) expire(now time.Time, ttl time.Duration) int {
	c.mutex.Lock()
	var released []reflect.Value
	c.lock.Lock()
	fetchers := c.fetchers.Elem()
	for id := range c.seen {
		if !fetchers.MapIndex(reflect.ValueOf(id)).IsValid() {
			delete(c.seen, id)
		}
	}
	for _, key := range fetchers.MapKeys() {
		id := key.String()
		first, ok := c.seen[id]
		if !ok {
			first = now
			c.seen[id] = now
		}
		if c.creating[id] == 0 && (ttl == 0 || now.Sub(first) >= ttl) {
			released = append(released, fetchers.MapIndex(key))
			fetchers.SetMapIndex(key, reflect.Value{})
			delete(c.seen, id)
		}
	}
	c.lock.Unlock()
	c.mutex.Unlock()
	for _, fetcher := range released {
		fetcher.MethodByName("Close").Call(nil)
	}
	return len(released)
}

func (c *resolveCache) close() {
	if c.stop != nil {
		close(c.stop)
		<-c.done
	}
	c.expire(time.Now(), 0)
}

// Keep a resolve result alive while a task is being created from it.
func (c *resolveCache) handler(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost || r.URL.Path != "/api/v1/tasks" {
			next.ServeHTTP(w, r)
			return
		}
		body, err := io.ReadAll(http.MaxBytesReader(w, r.Body, 1024*1024))
		if err != nil {
			http.Error(w, "invalid request", http.StatusBadRequest)
			return
		}
		r.Body = io.NopCloser(bytes.NewReader(body))
		var request struct {
			Rid string `json:"rid"`
		}
		if json.Unmarshal(body, &request) != nil || request.Rid == "" {
			next.ServeHTTP(w, r)
			return
		}
		c.mutex.Lock()
		c.creating[request.Rid]++
		c.mutex.Unlock()
		defer func() {
			c.mutex.Lock()
			if c.creating[request.Rid]--; c.creating[request.Rid] == 0 {
				delete(c.creating, request.Rid)
			}
			c.mutex.Unlock()
		}()
		next.ServeHTTP(w, r)
	})
}
