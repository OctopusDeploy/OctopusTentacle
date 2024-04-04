package main

import (
	"bufio"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"sync"
	"time"
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
	stdOutCmdReader, _ := cmd.StdoutPipe()
	stdErrCmdReader, _ := cmd.StderrPipe()

	stdOutScanner := bufio.NewScanner(stdOutCmdReader)
	stdErrScanner := bufio.NewScanner(stdErrCmdReader)

	doneStd := make(chan bool)
	doneErr := make(chan bool)

	go reader(stdOutScanner, "stdout", &doneStd, &lineCounter)
	go reader(stdErrScanner, "stderr", &doneErr, &lineCounter)

	Write("stdout", "##octopus[stdout-verbose]", &lineCounter)
	Write("stdout", "Kubernetes Script Pod started", &lineCounter)
	Write("stdout", "##octopus[stdout-default]", &lineCounter)

	err := cmd.Start()

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

	Write("stdout", "##octopus[stdout-verbose]", &lineCounter)
	Write("stdout", "Kubernetes Script Pod completed", &lineCounter)
	Write("stdout", "##octopus[stdout-default]", &lineCounter)

	//TODO: Add this back to speed things up
	//Write("stdout", fmt.Sprintf("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>%d", exitCode))

	os.Exit(exitCode)
}

func reader(scanner *bufio.Scanner, stream string, done *chan bool, counter *SafeCounter) {
	for scanner.Scan() {
		Write(stream, scanner.Text(), counter)
	}
	*done <- true
}

func Write(stream string, text string, counter *SafeCounter) {
	//Use a mutex to prevent race conditions updating the line number
	//https://go.dev/tour/concurrency/9
	counter.Mutex.Lock()

	fmt.Printf("%d|%s|%s|%s\n", counter.Value, time.Now().UTC().Format(time.RFC3339Nano), stream, text)
	counter.Value++

	counter.Mutex.Unlock()
}
