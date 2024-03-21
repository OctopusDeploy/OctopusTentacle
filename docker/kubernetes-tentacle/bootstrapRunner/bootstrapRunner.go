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

	go reader(stdOutScanner, "stdout", &doneStd)
	go reader(stdErrScanner, "stderr", &doneErr)

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

	fmt.Println("##octopus[stdout-verbose]")
	fmt.Println("Kubernetes Job completed")
	fmt.Println("##octopus[stdout-default]")

	fmt.Printf("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>%s", exitCode)

	os.Exit(exitCode)
}

func reader(scanner *bufio.Scanner, stream string, done *chan bool) {
	for scanner.Scan() {
		fmt.Printf("%s|%s|%s\n", time.Now().UTC().Format(time.RFC3339Nano), stream, scanner.Text())
	}
	*done <- true
}
