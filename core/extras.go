package main

import (
	"encoding/json"
	"fmt"
	"github.com/GopeedLab/gopeed/pkg/base"
	"github.com/GopeedLab/gopeed/pkg/download"
	"io"
	"net/http"
	"strings"
	"time"
)

func startExtras(d *download.Downloader, events *taskEvents, port int, token string) {
	config, err := d.GetConfig()
	if err != nil {
		return
	}
	if enabled, _ := config.Extra["autoStartTasks"].(bool); enabled {
		if err := d.Continue(&download.TaskFilter{NotStatuses: []base.Status{base.DownloadStatusDone}}); err != nil {
			d.Logger.Error().Err(err).Msg("resume downloads")
		}
	}
	events.add(func(event *download.Event) {
		if event.Key != download.EventKeyDone {
			return
		}
		cfg, err := d.GetConfig()
		if err != nil {
			return
		}
		if enabled, exists := cfg.Extra["desktopNotification"].(bool); exists && !enabled {
			return
		}
		notifyComplete(event.Task.Name())
	})
	go func() {
		update := func() {
			if err := updateTrackerSubscriptions(d, port, token); err != nil {
				d.Logger.Warn().Err(err).Msg("update tracker subscriptions")
			}
		}
		update()
		ticker := time.NewTicker(time.Hour)
		defer ticker.Stop()
		for range ticker.C {
			update()
		}
	}()
}
func updateTrackerSubscriptions(d *download.Downloader, port int, token string) error {
	cfg, err := d.GetConfig()
	if err != nil {
		return err
	}
	raw, ok := cfg.Extra["bt"].(map[string]any)
	if !ok {
		return nil
	}
	var subscriptions struct {
		URLs   []string   `json:"trackerSubscribeUrls"`
		Custom []string   `json:"customTrackers"`
		Auto   bool       `json:"autoUpdateTrackers"`
		Last   *time.Time `json:"lastTrackerUpdateTime"`
	}
	b, err := json.Marshal(raw)
	if err != nil {
		return err
	}
	if err = json.Unmarshal(b, &subscriptions); err != nil {
		return err
	}
	if !subscriptions.Auto || len(subscriptions.URLs) == 0 || (subscriptions.Last != nil && time.Since(*subscriptions.Last) < 24*time.Hour) {
		return nil
	}
	trackers := []string{}
	seen := map[string]bool{}
	client := http.Client{Timeout: 30 * time.Second}
	for _, source := range subscriptions.URLs {
		request, err := http.NewRequest("GET", fmt.Sprintf("http://127.0.0.1:%d/api/v1/proxy", port), nil)
		if err != nil {
			return err
		}
		request.Header.Set("X-Api-Token", token)
		request.Header.Set("X-Target-Uri", mirroredURL(source, cfg.Extra))
		resp, err := client.Do(request)
		if err != nil {
			return err
		}
		payload, readErr := io.ReadAll(io.LimitReader(resp.Body, 2*1024*1024))
		resp.Body.Close()
		if readErr != nil {
			return readErr
		}
		if resp.StatusCode != 200 {
			return fmt.Errorf("tracker subscription returned %d", resp.StatusCode)
		}
		for _, line := range strings.Fields(string(payload)) {
			if !seen[line] {
				seen[line] = true
				trackers = append(trackers, line)
			}
		}
	}
	latest, err := d.GetConfig()
	if err != nil {
		return err
	}
	latestBt, ok := latest.Extra["bt"].(map[string]any)
	if !ok {
		return nil
	}
	latestBt["subscribeTrackers"] = trackers
	latestBt["lastTrackerUpdateTime"] = time.Now().UTC().Format(time.RFC3339)
	var bt map[string]any
	b, err = json.Marshal(latest.ProtocolConfig["bt"])
	if err != nil {
		return err
	}
	if err = json.Unmarshal(b, &bt); err != nil {
		return err
	}
	if bt == nil {
		bt = map[string]any{}
	}
	latestCustom, _ := latestBt["customTrackers"].([]any)
	for _, value := range latestCustom {
		custom, _ := value.(string)
		if !seen[custom] {
			seen[custom] = true
			trackers = append(trackers, custom)
		}
	}
	bt["trackers"] = trackers
	latest.ProtocolConfig["bt"] = bt
	return d.PutConfig(latest)
}
