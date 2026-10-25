package main

import (
	"fmt"
	"github.com/GopeedLab/gopeed/pkg/base"
	"github.com/GopeedLab/gopeed/pkg/download"
	"sync"
	"time"
)

// Gopeed updates the task status before its asynchronous pause handler saves
// progress. Track the completion event so shutdown cannot close Bolt too early.
type lifecycle struct {
	downloader *download.Downloader
	mutex      sync.Mutex
	pending    map[string]bool
	changed    chan struct{}
}

func trackLifecycle(d *download.Downloader, events *taskEvents) *lifecycle {
	l := &lifecycle{downloader: d, pending: map[string]bool{}, changed: make(chan struct{}, 1)}
	events.add(func(event *download.Event) {
		l.mutex.Lock()
		switch event.Key {
		case download.EventKeyStart:
			l.pending[event.Task.ID] = true
		case download.EventKeyPause, download.EventKeyDone, download.EventKeyError, download.EventKeyDelete:
			delete(l.pending, event.Task.ID)
		}
		l.mutex.Unlock()
		select {
		case l.changed <- struct{}{}:
		default:
		}
	})
	return l
}

func (l *lifecycle) pauseAndWait() error {
	tasks := l.downloader.GetTasksByFilter(&download.TaskFilter{Statuses: []base.Status{base.DownloadStatusReady, base.DownloadStatusWait}})
	l.mutex.Lock()
	for _, task := range tasks {
		l.pending[task.ID] = true
	}
	l.mutex.Unlock()
	if err := l.downloader.Pause(nil); err != nil {
		return err
	}
	timeout := time.NewTimer(15 * time.Second)
	defer timeout.Stop()
	for {
		l.mutex.Lock()
		remaining := len(l.pending)
		l.mutex.Unlock()
		if remaining == 0 {
			return nil
		}
		select {
		case <-l.changed:
		case <-timeout.C:
			return fmt.Errorf("timed out while saving %d downloads", remaining)
		}
	}
}
