#! /usr/bin/bash

WORK_DIR=$1
STDOUT_LOG="$WORK_DIR/stdout.log"
STDERR_LOG="$WORK_DIR/stderr.log"

format() {
	now=$(date -u +"%Y-%m-%dT%H:%M:%S.%N%z")
	echo "$now|$2" | tee -a "$1"
}

logStdOut() {
  while read -r IN
  do
    format "$STDOUT_LOG" "$IN"
  done
}

logStdErr() {
  while read -r IN
  do
	format "$STDERR_LOG" "$IN"
  done
}

#ensure these files exist
rm -f "$STDOUT_LOG";
rm -f "$STDERR_LOG";
touch "$STDOUT_LOG"
touch "$STDERR_LOG"

#pass the remaining args (skipping the first which is the working directory)
shift

BOOTSTRAP_SCRIPT=$1

#This is the args for the Bootstrap script
shift

exec > >(logStdOut)
exec 2> >(logStdErr >&2)

/bin/bash $BOOTSTRAP_SCRIPT "$@"

sleep 1