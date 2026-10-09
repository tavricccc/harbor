package localization

import (
	"embed"
	"encoding/json"
	"fmt"
	"sync/atomic"
)

//go:embed *.json
var resources embed.FS
var current atomic.Pointer[map[string]string]

func Set(language string) error {
	payload, err := resources.ReadFile(language + ".json")
	if err != nil {
		return fmt.Errorf("unsupported language: %s", language)
	}
	var catalog map[string]string
	if err := json.Unmarshal(payload, &catalog); err != nil {
		return err
	}
	current.Store(&catalog)
	return nil
}

func Text(key string) string {
	return (*current.Load())[key]
}
