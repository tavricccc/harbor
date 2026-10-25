package main

import (
	"testing"

	"github.com/GopeedLab/gopeed/pkg/download"
)

func TestTaskEventsReachEveryListener(t *testing.T) {
	events := &taskEvents{}
	lifecycle := trackLifecycle(nil, events)
	notified := 0
	events.add(func(event *download.Event) { notified++ })
	task := &download.Task{ID: "task"}
	events.emit(&download.Event{Key: download.EventKeyStart, Task: task})
	if !lifecycle.pending["task"] || notified != 1 {
		t.Fatal("a later listener must not replace shutdown tracking")
	}
	events.emit(&download.Event{Key: download.EventKeyDone, Task: task})
	if len(lifecycle.pending) != 0 || notified != 2 {
		t.Fatal("completion must reach shutdown tracking and other listeners")
	}
}
