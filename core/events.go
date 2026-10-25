package main

import (
	"sync"

	"github.com/GopeedLab/gopeed/pkg/download"
)

// Gopeed keeps a single task listener and a later registration replaces the
// earlier one. Register once and fan events out to every Harbor consumer.
type taskEvents struct {
	mutex     sync.RWMutex
	listeners []download.Listener
}

func listenTasks(d *download.Downloader) *taskEvents {
	events := &taskEvents{}
	d.Listener(events.emit)
	return events
}

func (e *taskEvents) add(listener download.Listener) {
	e.mutex.Lock()
	defer e.mutex.Unlock()
	e.listeners = append(e.listeners, listener)
}

func (e *taskEvents) emit(event *download.Event) {
	e.mutex.RLock()
	listeners := e.listeners
	e.mutex.RUnlock()
	for _, listener := range listeners {
		listener(event)
	}
}
