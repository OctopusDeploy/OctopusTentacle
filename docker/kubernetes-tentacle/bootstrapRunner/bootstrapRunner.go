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

	if _, err := stdoutLogFile.WriteString(format("Script starting")); err != nil {
		panic(err)
	}

	err = cmd.Start()
	if err != nil {
		panic(err)
	}

	// Wait for output buffering first
	<-doneStd
	<-doneErr

	err = cmd.Wait()

	if _, err := stdoutLogFile.WriteString(format("Script Completed")); err != nil {
		panic(err)
	}

	var exitErr *exec.ExitError
	// If the error is not related to the script returning a failure exit code we log it.
	if err != nil && !errors.As(err, &exitErr) {
		fmt.Fprintln(os.Stderr, "bootstrapRunner.go: Failed to execute bootstrap script", err)
		//stderrLogFile.WriteString("bootstrapRunner.go: Failed to execute bootstrap script" + err)
	}

	eosMarker := format("End of script 075CD4F0-8C76-491D-BA76-0879D35E9CFE")
	if _, err := stdoutLogFile.WriteString(eosMarker); err != nil {
		panic(err)
	}
	fmt.Fprintln(os.Stdout, eosMarker)

	if err := stdoutLogFile.Flush(); err != nil {
		panic(err)
	}

	os.Exit(cmd.ProcessState.ExitCode())
}

func format(line string) string {
	return fmt.Sprintf("%s|%s\n", time.Now().UTC().Format(time.RFC3339Nano), line)
}

func reader(scanner *bufio.Scanner, writer *bufio.Writer, done *chan bool) {
	for scanner.Scan() {
		message := format(scanner.Text())
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
