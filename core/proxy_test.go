package main

import (
	"io"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestTextProxy(t *testing.T) {
	target := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		io.WriteString(w, "udp://tracker.example:80/announce")
	}))
	defer target.Close()
	proxy := httptest.NewServer(textProxy(http.NotFoundHandler(), "secret"))
	defer proxy.Close()
	fetch := func(token, uri string) (int, string) {
		request, _ := http.NewRequest(http.MethodGet, proxy.URL+"/api/v1/proxy", nil)
		request.Header.Set("X-Api-Token", token)
		request.Header.Set("X-Target-Uri", uri)
		response, err := http.DefaultClient.Do(request)
		if err != nil {
			t.Fatal(err)
		}
		defer response.Body.Close()
		body, _ := io.ReadAll(response.Body)
		return response.StatusCode, string(body)
	}
	if status, body := fetch("secret", target.URL); status != http.StatusOK || body != "udp://tracker.example:80/announce" {
		t.Fatalf("proxy returned %d %q", status, body)
	}
	if status, _ := fetch("wrong", target.URL); status != http.StatusUnauthorized {
		t.Fatalf("unauthorized proxy returned %d", status)
	}
	if status, _ := fetch("secret", "file:///C:/Windows/win.ini"); status != http.StatusBadRequest {
		t.Fatalf("file proxy returned %d", status)
	}
}
