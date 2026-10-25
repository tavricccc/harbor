package main

import (
	"io"
	"net/http"
	"net/url"
	"time"
)

// Upstream removed its text proxy; Harbor still fetches update, extension store
// and tracker lists through the core so they share its network environment.
func textProxy(next http.Handler, token string) http.Handler {
	client := &http.Client{Timeout: 30 * time.Second}
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet || r.URL.Path != "/api/v1/proxy" {
			next.ServeHTTP(w, r)
			return
		}
		if r.Header.Get("X-Api-Token") != token {
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}
		target, err := url.Parse(r.Header.Get("X-Target-Uri"))
		if err != nil || (target.Scheme != "http" && target.Scheme != "https") || target.Host == "" {
			http.Error(w, "invalid target", http.StatusBadRequest)
			return
		}
		request, err := http.NewRequestWithContext(r.Context(), http.MethodGet, target.String(), nil)
		if err != nil {
			http.Error(w, "invalid target", http.StatusBadRequest)
			return
		}
		request.Header.Set("User-Agent", "Harbor")
		response, err := client.Do(request)
		if err != nil {
			http.Error(w, err.Error(), http.StatusBadGateway)
			return
		}
		defer response.Body.Close()
		if contentType := response.Header.Get("Content-Type"); contentType != "" {
			w.Header().Set("Content-Type", contentType)
		}
		w.WriteHeader(response.StatusCode)
		io.Copy(w, io.LimitReader(response.Body, 8*1024*1024))
	})
}
