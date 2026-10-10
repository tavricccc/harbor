package main

import (
	"bytes"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"syscall"
	"time"

	"harbor/core/internal/foreground"
)

type message struct {
	Method string          `json:"method"`
	Meta   map[string]any  `json:"meta"`
	Params json.RawMessage `json:"params"`
}
type response struct {
	Code    int    `json:"code"`
	Data    any    `json:"data,omitempty"`
	Message string `json:"message,omitempty"`
}
type session struct {
	Port  int    `json:"port"`
	Token string `json:"token"`
	PID   int    `json:"pid"`
}

var client = &http.Client{Timeout: 60 * time.Second}
var dataDir = filepath.Join(os.Getenv("LOCALAPPDATA"), "Harbor")
var grantForeground = foreground.Allow

func init() {
	if root := os.Getenv("HARBOR_DATA_DIRECTORY"); root != "" {
		dataDir = root
	}
}

func current() (*session, error) {
	b, err := os.ReadFile(filepath.Join(dataDir, "session.json"))
	if err != nil {
		return nil, err
	}
	var state session
	if err = json.Unmarshal(b, &state); err != nil {
		return nil, err
	}
	return &state, nil
}
func request(state *session, method, route string, body []byte) ([]byte, error) {
	req, err := http.NewRequest(method, fmt.Sprintf("http://127.0.0.1:%d/api/v1/%s", state.Port, route), bytes.NewReader(body))
	if err != nil {
		return nil, err
	}
	if req.Method == http.MethodPost && req.URL.Path == "/api/v1/tasks" {
		grantForeground(state.PID)
	}
	req.Header.Set("X-Api-Token", state.Token)
	req.Header.Set("Content-Type", "application/json")
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return nil, fmt.Errorf("request failed: %d", resp.StatusCode)
	}
	return io.ReadAll(resp.Body)
}
func alive() bool {
	state, err := current()
	if err != nil {
		return false
	}
	_, err = request(state, "GET", "info", nil)
	return err == nil
}
func ensureCore() error {
	if alive() {
		return nil
	}
	exe, err := os.Executable()
	if err != nil {
		return err
	}
	engine := filepath.Dir(exe)
	root := filepath.Dir(engine)
	var preferences struct{ ApiPort int }
	preferences.ApiPort = 18762
	if b, err := os.ReadFile(filepath.Join(dataDir, "preferences.json")); err == nil {
		if err = json.Unmarshal(b, &preferences); err != nil {
			return err
		}
	}
	command := exec.Command(filepath.Join(engine, "harbor-core.exe"), "--data", dataDir, "--ui", filepath.Join(root, "Harbor.exe"), "--icon", filepath.Join(root, "Assets", "AppIcon.ico"), "--port", fmt.Sprint(preferences.ApiPort))
	command.Env = append(os.Environ(), "TORRENT_STORAGE_DEFAULT_FILE_IO=classic")
	command.SysProcAttr = &syscall.SysProcAttr{HideWindow: true}
	if err = command.Start(); err != nil {
		return err
	}
	command.Process.Release()
	deadline := time.Now().Add(20 * time.Second)
	for time.Now().Before(deadline) {
		if alive() {
			return nil
		}
		time.Sleep(100 * time.Millisecond)
	}
	return fmt.Errorf("Harbor could not start")
}
func handle(m message) (any, error) {
	if m.Method == "ping" {
		return alive(), nil
	}
	if err := ensureCore(); err != nil {
		return nil, err
	}
	if m.Method == "wakeup" {
		if silent, _ := m.Meta["silent"].(bool); !silent {
			exe, err := os.Executable()
			if err != nil {
				return nil, err
			}
			command := exec.Command(filepath.Join(filepath.Dir(filepath.Dir(exe)), "Harbor.exe"))
			if err = command.Start(); err != nil {
				return nil, err
			}
			grantForeground(command.Process.Pid)
			command.Process.Release()
		}
		return nil, nil
	}
	state, err := current()
	if err != nil {
		return nil, err
	}
	if m.Method == "create" {
		var encoded string
		if err = json.Unmarshal(m.Params, &encoded); err != nil {
			return nil, fmt.Errorf("invalid create payload")
		}
		payload, decodeErr := base64.StdEncoding.DecodeString(encoded)
		if decodeErr != nil {
			return nil, decodeErr
		}
		body, sendErr := request(state, "POST", "tasks", payload)
		if sendErr != nil {
			return nil, sendErr
		}
		var result struct {
			Code    int    `json:"code"`
			Message string `json:"msg"`
		}
		if err = json.Unmarshal(body, &result); err != nil {
			return nil, err
		}
		if result.Code != 0 {
			return nil, fmt.Errorf("%s", result.Message)
		}
		return nil, err
	}
	if m.Method != "forward" {
		return nil, fmt.Errorf("unknown method")
	}
	var params struct {
		Path            string          `json:"path"`
		Method          string          `json:"method"`
		Data            json.RawMessage `json:"data"`
		Query           map[string]any  `json:"query"`
		QueryParameters map[string]any  `json:"queryParameters"`
	}
	if err = json.Unmarshal(m.Params, &params); err != nil {
		return nil, err
	}
	route, err := url.Parse(params.Path)
	if err != nil || route.IsAbs() || route.Host != "" {
		return nil, fmt.Errorf("invalid API path")
	}
	query := route.Query()
	for key, value := range params.Query {
		addQuery(query, key, value)
	}
	for key, value := range params.QueryParameters {
		addQuery(query, key, value)
	}
	route.RawQuery = query.Encode()
	path := route.String()
	for len(path) > 0 && path[0] == '/' {
		path = path[1:]
	}
	if len(path) >= 7 && path[:7] == "api/v1/" {
		path = path[7:]
	}
	if params.Method == "" {
		params.Method = "GET"
	}
	payload, err := request(state, params.Method, path, params.Data)
	if err != nil {
		return nil, err
	}
	return json.RawMessage(payload), nil
}
func addQuery(query url.Values, key string, value any) {
	query.Del(key)
	if list, ok := value.([]any); ok {
		for _, item := range list {
			query.Add(key, fmt.Sprint(item))
		}
	} else {
		query.Set(key, fmt.Sprint(value))
	}
}
func main() {
	for {
		var size uint32
		if err := binary.Read(os.Stdin, binary.LittleEndian, &size); err != nil {
			return
		}
		if size == 0 || size > 1024*1024 {
			return
		}
		payload := make([]byte, size)
		if _, err := io.ReadFull(os.Stdin, payload); err != nil {
			return
		}
		var m message
		var result response
		if err := json.Unmarshal(payload, &m); err != nil {
			result.Code = 1000
			result.Message = "Invalid request"
		} else {
			value, err := handle(m)
			if err != nil {
				result.Code = 1000
				result.Message = err.Error()
			} else {
				result.Data = value
			}
		}
		output, err := json.Marshal(result)
		if err != nil {
			return
		}
		if err = binary.Write(os.Stdout, binary.LittleEndian, uint32(len(output))); err != nil {
			return
		}
		if _, err = os.Stdout.Write(output); err != nil {
			return
		}
	}
}
