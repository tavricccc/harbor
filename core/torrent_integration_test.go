package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/binary"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"testing"
	"time"

	"github.com/GopeedLab/gopeed/pkg/base"
	"github.com/GopeedLab/gopeed/pkg/download"
	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
	"github.com/anacrolix/torrent"
	"github.com/anacrolix/torrent/bencode"
	"github.com/anacrolix/torrent/metainfo"
)

func TestLocalTorrentSelectedFileTransfer(t *testing.T) {
	// The engine owns process-global BT clients and mmap storage. Exercise the
	// same process boundary as the shipped core, then clean up after it exits.
	root := os.Getenv("HARBOR_BT_TEST_ROOT")
	if root == "" {
		root = t.TempDir()
		command := exec.Command(os.Args[0], "-test.run=^TestLocalTorrentSelectedFileTransfer$", "-test.v")
		command.Env = append(os.Environ(), "HARBOR_BT_TEST_ROOT="+root)
		command.Dir = root
		if output, err := command.CombinedOutput(); err != nil {
			t.Fatalf("isolated BitTorrent core: %v\n%s", err, output)
		}
		return
	}
	source := filepath.Join(root, "seed", "fixture")
	if err := os.MkdirAll(source, 0700); err != nil {
		t.Fatal(err)
	}
	payload := bytes.Repeat([]byte("Harbor local BitTorrent verification\n"), 8192)
	for _, name := range []string{"a.bin", "b.bin"} {
		if err := os.WriteFile(filepath.Join(source, name), payload, 0600); err != nil {
			t.Fatal(err)
		}
	}
	info := metainfo.Info{PieceLength: 16384}
	if err := info.BuildFromFilePath(source); err != nil {
		t.Fatal(err)
	}
	seedConfig := torrent.NewDefaultClientConfig()
	seedConfig.DataDir = filepath.Dir(source)
	seedConfig.ListenPort = 0
	// This fixture serves peers over TCP; avoid allocating a paired UDP port,
	// which can be excluded independently on hosted Windows runners.
	seedConfig.DisableUTP = true
	seedConfig.DisableIPv6 = true
	seedConfig.NoDHT = true
	seedConfig.DisableTrackers = true
	seedConfig.Seed = true
	seedConfig.ListenHost = func(string) string { return "127.0.0.1" }
	seeder, err := torrent.NewClient(seedConfig)
	if err != nil {
		t.Fatal(err)
	}
	defer seeder.Close()
	var seedPort int
	for _, address := range seeder.ListenAddrs() {
		if tcp, ok := address.(*net.TCPAddr); ok {
			seedPort = tcp.Port
			break
		}
	}
	if seedPort == 0 {
		t.Fatal("local seeder has no TCP listener")
	}
	tracker := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		peer := []byte{127, 0, 0, 1, 0, 0}
		binary.BigEndian.PutUint16(peer[4:], uint16(seedPort))
		w.Write(bencode.MustMarshal(map[string]any{"interval": 1, "complete": 1, "incomplete": 0, "peers": string(peer)}))
	}))
	defer tracker.Close()
	metadata := metainfo.MetaInfo{Announce: tracker.URL, InfoBytes: bencode.MustMarshal(info)}
	seeded, err := seeder.AddTorrent(&metadata)
	if err != nil {
		t.Fatal(err)
	}
	seeded.DownloadAll()
	torrentPath := filepath.Join(root, "fixture.torrent")
	file, err := os.Create(torrentPath)
	if err != nil {
		t.Fatal(err)
	}
	err = metadata.Write(file)
	file.Close()
	if err != nil {
		t.Fatal(err)
	}
	_, err = rest.Start(&model.StartConfig{Address: "127.0.0.1:0", Storage: model.StorageBolt, StorageDir: filepath.Join(root, "state"), ApiToken: "test", ProductionMode: true})
	if err != nil {
		t.Fatal(err)
	}
	defer rest.Stop()
	destination := filepath.Join(root, "downloads")
	resolved, err := rest.Downloader.Resolve(&base.Request{URL: torrentPath}, &base.Options{Path: destination, SelectFiles: []int{0}})
	if err != nil {
		t.Fatal(err)
	}
	if len(resolved.Res.Files) != 2 || resolved.Res.Files[0].Name != "a.bin" {
		t.Fatal("multi-file torrent did not resolve")
	}
	id, err := rest.Downloader.Create(resolved.ID)
	if err != nil {
		t.Fatal(err)
	}
	defer func() {
		if err := rest.Downloader.Delete(&download.TaskFilter{IDs: []string{id}}, false); err != nil {
			t.Error(err)
		}
	}()
	deadline := time.Now().Add(30 * time.Second)
	for time.Now().Before(deadline) {
		tasks := rest.Downloader.GetTasksByFilter(&download.TaskFilter{IDs: []string{id}})
		if len(tasks) == 1 && tasks[0].Status == base.DownloadStatusDone {
			actual, err := os.ReadFile(filepath.Join(destination, "fixture", "a.bin"))
			if err != nil {
				t.Fatal(err)
			}
			if sha256.Sum256(actual) != sha256.Sum256(payload) {
				t.Fatal("BitTorrent payload checksum mismatch")
			}
			if data, _ := os.ReadFile(filepath.Join(destination, "fixture", "b.bin")); bytes.Equal(data, payload) {
				t.Fatal("unselected file was downloaded")
			}
			return
		}
		time.Sleep(50 * time.Millisecond)
	}
	t.Fatal("local BitTorrent transfer did not complete within 30 seconds")
}
