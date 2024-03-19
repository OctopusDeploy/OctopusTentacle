package main

import (
	"bufio"
	"errors"
	"fmt"
	"os"
	"os/exec"
	"time"
)

// The bootstrapRunner applet is designed to execute a script in a specific folder
// and format the script's output from stdout and stderr into the following format:
// <line number>|<RFC3339Nano date time>|<"stdout" or "stderr">|<original string>
//
// The applet can be executed as such:
// bootstrapRunner <working directory> <script> <script arguments>...
//
// Note: all arguments given after the <script> argument are passed directly to the script as arguments.
func main() {
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

	//stdout log file
	so, err := os.Create(workspacePath + "/stdout.log")
	if err != nil {
		panic(err)
	}
	// close fo on exit and check for its returned error
	defer func() {
		if err := so.Close(); err != nil {
			panic(err)
		}
	}()
	// make a write buffer
	stdoutLogFile := bufio.NewWriter(so)

	//stderr log file
	se, err := os.Create(workspacePath + "/stderr.log")
	if err != nil {
		panic(err)
	}
	// close fo on exit and check for its returned error
	defer func() {
		if err := se.Close(); err != nil {
			panic(err)
		}
	}()
	// make a write buffer
	stderrLogFile := bufio.NewWriter(se)

	go reader(stdOutScanner, stdoutLogFile, &doneStd)
	go reader(stdErrScanner, stderrLogFile, &doneErr)

	err = cmd.Start()
	if err != nil {
		panic(err)
	}

	// Wait for output buffering first
	<-doneStd
	<-doneErr

	err = cmd.Wait()

	var exitErr *exec.ExitError
	// If the error is not related to the script returning a failure exit code we log it.
	if err != nil && !errors.As(err, &exitErr) {
		fmt.Fprintln(os.Stderr, "bootstrapRunner.go: Failed to execute bootstrap script", err)
	}

	stdoutLogFile.Flush()
	stderrLogFile.Flush()

	os.Exit(cmd.ProcessState.ExitCode())
}

func reader(scanner *bufio.Scanner, writer *bufio.Writer, done *chan bool) {
	for scanner.Scan() {
		message := fmt.Sprintf("%s|%s\n", time.Now().UTC().Format(time.RFC3339Nano), scanner.Text())
		fmt.Print(message)
		if _, err := writer.WriteString(message); err != nil {
			panic(err)
		}

		if err := writer.Flush(); err != nil {
			panic(err)
		}
	}
	*done <- true
}
