package main

import (
	"fmt"
	"net"
	"os"
	"os/signal"
	"path/filepath"
	"time"

	"github.com/GopeedLab/gopeed/pkg/rest"
	"github.com/GopeedLab/gopeed/pkg/rest/model"
	"harbor/localization"
)

func runCore(options coreOptions) error {
	if err := os.MkdirAll(options.root, 0700); err != nil {
		return err
	}
	if err := localization.Set(options.language); err != nil {
		return err
	}
	release, acquired := acquireCore(options.root)
	if !acquired {
		return nil
	}
	defer release()
	token, err := apiToken(options.root)
	if err != nil {
		return err
	}
	api, listener, err := rest.BuildServer(&model.StartConfig{
		Address: fmt.Sprintf("127.0.0.1:%d", options.apiPort), Storage: model.StorageBolt,
		StorageDir: options.root, ApiToken: token, ProductionMode: true, RefreshInterval: 1000,
	})
	if err != nil {
		return err
	}
	defer api.Close()
	defer rest.Stop()
	port := listener.Addr().(*net.TCPAddr).Port
	api.Handler = browserConfirmation(api.Handler, token, func(body []byte) (string, error) {
		return openDownloadRequest(options.root, options.ui, body)
	})
	api.Handler = localExtensions(api.Handler, token)
	queue, err := loadDeferredDownloads(options.root, createDeferredTask)
	if err != nil {
		return err
	}
	api.Handler = queue.handler(api.Handler, token)
	queueStop, queueDone := startDeferredQueue(queue)
	go api.Serve(listener)
	lifecycle := trackLifecycle(rest.Downloader)
	if err := configureDownloadFolder(); err != nil {
		return err
	}
	stop := make(chan os.Signal, 1)
	signal.Notify(stop, os.Interrupt)
	defer signal.Stop(stop)
	control, controlPort, err := startControl(token, stop)
	if err != nil {
		return err
	}
	defer control.Close()
	sessionPath, err := saveSession(options.root, session{port, token, os.Getpid(), controlPort})
	if err != nil {
		return err
	}
	defer os.Remove(sessionPath)
	startExtras(rest.Downloader, port, token)
	fmt.Println("Harbor core ready")
	if options.ui == "" {
		<-stop
	} else {
		runTray(options.ui, options.icon, stop)
	}
	close(queueStop)
	<-queueDone
	if err := lifecycle.pauseAndWait(); err != nil {
		rest.Downloader.Logger.Error().Err(err).Msg("save before shutdown failed")
	}
	return nil
}

func createDeferredTask(request model.CreateTask) (string, error) {
	// Recover a saved handoff before creating a second download.
	if id := request.Req.Labels["harborDeferredId"]; id != "" {
		for _, task := range rest.Downloader.GetTasks() {
			if task.Meta.Req.Labels["harborDeferredId"] == id {
				return task.ID, nil
			}
		}
	}
	return rest.Downloader.CreateDirect(request.Req, request.Opts)
}

func startDeferredQueue(queue *deferredDownloads) (chan struct{}, chan struct{}) {
	stop, done := make(chan struct{}), make(chan struct{})
	go func() {
		defer close(done)
		ticker := time.NewTicker(time.Second)
		defer ticker.Stop()
		for {
			select {
			case now := <-ticker.C:
				queue.runDue(now)
			case <-stop:
				return
			}
		}
	}()
	return stop, done
}

func configureDownloadFolder() error {
	config, err := rest.Downloader.GetConfig()
	if err != nil {
		return err
	}
	if config.DownloadDir != "" {
		return nil
	}
	home, err := os.UserHomeDir()
	if err != nil {
		return err
	}
	config.DownloadDir = filepath.Join(home, "Downloads")
	return rest.Downloader.PutConfig(config)
}
