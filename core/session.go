package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"os"
	"path/filepath"
)

type session struct {
	Port        int    `json:"port"`
	Token       string `json:"token"`
	PID         int    `json:"pid"`
	ControlPort int    `json:"controlPort"`
}

func apiToken(root string) (string, error) {
	path := filepath.Join(root, "api-token")
	payload, err := os.ReadFile(path)
	if os.IsNotExist(err) {
		secret := make([]byte, 32)
		if _, err := rand.Read(secret); err != nil {
			return "", err
		}
		payload = []byte(hex.EncodeToString(secret))
		if err := os.WriteFile(path, payload, 0600); err != nil {
			return "", err
		}
	} else if err != nil {
		return "", err
	}
	return string(payload), nil
}

func saveSession(root string, state session) (string, error) {
	payload, err := json.Marshal(state)
	if err != nil {
		return "", err
	}
	path := filepath.Join(root, "session.json")
	if err := os.WriteFile(path+".tmp", payload, 0600); err != nil {
		return "", err
	}
	if err := os.Rename(path+".tmp", path); err != nil {
		return "", err
	}
	return path, nil
}
