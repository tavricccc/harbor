package main

import (
	"bytes"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"testing"
	"time"

	"github.com/GopeedLab/gopeed/pkg/base"
	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
)

// Abandoned probes must release their prefetch file on expiry and shutdown,
// while a task being created from a probe keeps it.
func TestResolveCacheReleasesPrefetch(t *testing.T) {
	payload := bytes.Repeat([]byte("Harbor resolve cache\n"), 200000)
	fixture := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		http.ServeContent(pacedWriter{w}, r, "fixture.bin", time.Unix(0, 0), bytes.NewReader(payload))
	}))
	defer fixture.Close()
	root := t.TempDir()
	temp := filepath.Join(root, "temp")
	if _, err := rest.Start(&model.StartConfig{Address: "127.0.0.1:0", Storage: model.StorageBolt, StorageDir: root, TempDir: temp, ApiToken: "test", ProductionMode: false}); err != nil {
		t.Fatal(err)
	}
	defer rest.Stop()
	cache, err := trackResolves(rest.Downloader)
	if err != nil {
		t.Fatal(err)
	}
	prefetchFiles := func() int {
		matches, _ := filepath.Glob(filepath.Join(temp, "gopeed-prefetch-*"))
		return len(matches)
	}
	resolve := func() string {
		resolved, err := rest.Downloader.Resolve(&base.Request{URL: fixture.URL + "/fixture.bin"})
		if err != nil {
			t.Fatal(err)
		}
		return resolved.ID
	}
	waitFiles := func(want int) {
		deadline := time.Now().Add(10 * time.Second)
		for prefetchFiles() != want && time.Now().Before(deadline) {
			time.Sleep(20 * time.Millisecond)
		}
		if got := prefetchFiles(); got != want {
			t.Fatalf("prefetch files = %d, want %d", got, want)
		}
	}

	expired := resolve()
	waitFiles(1)
	now := time.Now()
	if released := cache.expire(now, resolveTTL); released != 0 {
		t.Fatalf("fresh resolve released: %d", released)
	}
	if released := cache.expire(now.Add(resolveTTL), resolveTTL); released != 1 {
		t.Fatalf("expired resolves released = %d, want 1", released)
	}
	waitFiles(0)
	if _, err := rest.Downloader.Create(expired); err == nil {
		t.Fatal("expired resolve still creates a task")
	}

	creating := resolve()
	abandoned := resolve()
	waitFiles(2)
	cache.creating[creating]++
	if released := cache.expire(time.Now(), 0); released != 1 {
		t.Fatalf("shutdown released = %d, want only the abandoned resolve", released)
	}
	waitFiles(1)
	if _, err := rest.Downloader.Create(abandoned); err == nil {
		t.Fatal("released resolve still creates a task")
	}
	cache.creating[creating]--
	cache.close()
	waitFiles(0)
}
