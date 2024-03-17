package main

import (
	"bufio"
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

	go reader(stdOutScanner, "stdout", &doneStd)
	go reader(stdErrScanner, "stderr", &doneErr)

	err := cmd.Start()
	if err != nil {
		panic(err)
	}

	// Wait for output buffering first
	<-doneStd
	<-doneErr

	err = cmd.Wait()
	if err != nil {
		fmt.Fprintln(os.Stderr, "Error waiting for Cmd", err)
		os.Exit(1)
	}
}

func reader(scanner *bufio.Scanner, logLevel string, done *chan bool) {
	for scanner.Scan() {
		fmt.Printf("%s|%s|%s\n", time.Now().UTC().Format(time.RFC3339Nano), logLevel, scanner.Text())
	}
	*done <- true
}
