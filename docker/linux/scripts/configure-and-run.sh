#!/bin/bash
set -eu

/scripts/configure-tentacle.sh

exec /scripts/run-tentacle.sh
