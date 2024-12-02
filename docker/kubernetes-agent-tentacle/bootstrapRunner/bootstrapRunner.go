package main

import (
	"bufio"
	"crypto/aes"
	"crypto/cipher"
	"crypto/rand"
	"encoding/base64"
	"errors"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path"
	"sync"
)

type SafeCounter struct {
	Mutex sync.Mutex
	Value int
}

// The bootstrapRunner applet is designed to execute a script in a specific folder
// and format the script's output from stdout and stderr into the following format:
// <line number>|<RFC3339Nano date time>|<"stdout" or "stderr">|<original string>
//
// The applet can be executed as such:
// bootstrapRunner <working directory> <script> <script arguments>...
//
// Note: all arguments given after the <script> argument are passed directly to the script as arguments.
func main() {

	lineCounter := SafeCounter{Value: 1, Mutex: sync.Mutex{}}

	workspacePath := os.Args[1]
	args := os.Args[2:]
	cmd := exec.Command("bash", args[0:]...)
	cmd.Dir = workspacePath

	gcm, err := CreateCipher(workspacePath)
	if err != nil {
		panic(err)
	}

	stdOutCmdReader, _ := cmd.StdoutPipe()
	stdErrCmdReader, _ := cmd.StderrPipe()

	stdOutScanner := bufio.NewScanner(stdOutCmdReader)
	stdErrScanner := bufio.NewScanner(stdErrCmdReader)

	doneStd := make(chan bool)
	doneErr := make(chan bool)

	go reader(stdOutScanner, "stdout", &doneStd, &lineCounter, gcm)
	go reader(stdErrScanner, "stderr", &doneErr, &lineCounter, gcm)

	Write("stdout", "##octopus[stdout-verbose]", &lineCounter, gcm)
	Write("stdout", "Kubernetes Script Pod started", &lineCounter, gcm)
	Write("stdout", "##octopus[stdout-default]", &lineCounter, gcm)

	err = cmd.Start()

	// Wait for output buffering first
	<-doneStd
	<-doneErr

	if err != nil {
		panic(err)
	}

	err = cmd.Wait()

	var exitErr *exec.ExitError
	// If the error is not related to the script returning a failure exit code we log it.
	if err != nil && !errors.As(err, &exitErr) {
		fmt.Fprintln(os.Stderr, "bootstrapRunner.go: Failed to execute bootstrap script", err)
	}

	exitCode := cmd.ProcessState.ExitCode()

	Write("stdout", "##octopus[stdout-verbose]", &lineCounter, gcm)
	Write("stdout", "Kubernetes Script Pod completed", &lineCounter, gcm)
	Write("stdout", "##octopus[stdout-default]", &lineCounter, gcm)

	Write("debug", fmt.Sprintf("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>%d", exitCode), &lineCounter, gcm)

	os.Exit(exitCode)
}

func reader(scanner *bufio.Scanner, stream string, done *chan bool, counter *SafeCounter, gcm cipher.AEAD) {
	for scanner.Scan() {
		Write(stream, scanner.Text(), counter, gcm)
	}
	*done <- true
}

func Write(stream string, text string, counter *SafeCounter, gcm cipher.AEAD) {
	//Use a mutex to prevent race conditions updating the line number
	//https://go.dev/tour/concurrency/9
	counter.Mutex.Lock()

	nonce := make([]byte, gcm.NonceSize())
	if _, err := io.ReadFull(rand.Reader, nonce); err != nil {
		panic(err)
	}

	ciphertext := gcm.Seal(nonce, nonce, []byte(text), nil)

	// the |e| indicates the line is encrypted (if we every supported plain, we'd put |p| here)
	fmt.Printf("|e|%d|%s|%x\n", counter.Value, stream, ciphertext)
	counter.Value++

	counter.Mutex.Unlock()
}

func CreateCipher(workspaceDir string) (cipher.AEAD, error) {
	// Read the key from the file
	fileBytes, err := os.ReadFile(path.Join(workspaceDir, "keyfile"))
	if err != nil {
		return nil, err
	}

	//the key is encoded in the file in Base64
	key := make([]byte, base64.StdEncoding.DecodedLen(len(fileBytes)))
	length, err := base64.StdEncoding.Decode(key, fileBytes)
	if err != nil {
		return nil, err
	}

	// use the decoded length to slice the array to the correct length (removes padding bytes)
	key = key[:length]

	// Ensure the key length is valid for AES (16, 24, or 32 bytes for AES-128, AES-192, or AES-256)
	keyLength := len(key)
	if keyLength != 16 && keyLength != 24 && keyLength != 32 {
		return nil, fmt.Errorf("invalid key size: %d bytes. Key must be 16, 24, or 32 bytes", keyLength)
	}
	// Create the AES cipher
	block, err := aes.NewCipher(key)
	if err != nil {
		return nil, err
	}

	//we specify a known 12 byte nonce size so we can easily retrieve it in Tentacle
	gcm, err := cipher.NewGCMWithNonceSize(block, 12)
	if err != nil {
		return nil, err
	}

	return gcm, nil
}
