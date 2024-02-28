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

# Change cwd to the working directory
cd "$WORK_DIR" || return

# Write a message to say the job is starting
echo "##octopus[stdout-default]"
echo "Kubernetes Pod starting" $(date)
echo "##octopus[stdout-default]"

/bin/bash "$BOOTSTRAP_SCRIPT" "$@"

#Get the return value from the previous script
RETURN_VAL=$?

# Write a message to say the job has completed
echo "##octopus[stdout-default]"
echo "Kubernetes Pod completed" $(date)
echo "##octopus[stdout-default]"

echo "End of script 075CD4F0-8C76-491D-BA76-0879D35E9CFE" $RETURN_VAL

# This ungodly hack is to stop the pod from being killed before the last log has been flushed
sleep 0.250 #250ms

echo "Hahahahhaha Probably missing"

#Propagate the return value from the bootstrap script to the output host
exit "$RETURN_VAL"