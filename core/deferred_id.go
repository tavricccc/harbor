package main

import (
	"crypto/rand"
	"encoding/hex"
)

func newDeferredID() string {
	var bytes [16]byte
	if _, err := rand.Read(bytes[:]); err != nil {
		panic(err)
	}
	return hex.EncodeToString(bytes[:])
}
