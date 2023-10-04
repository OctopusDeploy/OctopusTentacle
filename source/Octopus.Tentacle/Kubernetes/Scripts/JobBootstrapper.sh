#! /bin/bash

Log() {
  DateTime=$(date -u -Ins | sed s/,/./)
  echo -n '["'"$1"'","'"$2"'","'"$DateTime"'"]' >> "$TentacleWork"/Output.log
}

LogStdOut() {
  while read -r IN
  do Log "stdout" "$IN";
  done
}

LogStdErr() {
  while read -r IN;
  do Log "stderr" "$IN";
  done
}

bash "$@" 2> >(LogStdErr >&2) 1> >(LogStdOut >&1)