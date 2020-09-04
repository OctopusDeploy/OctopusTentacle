#!/bin/bash
set -ex

tentacle agent --instance "$OCTOPUS_TENTACLE_INSTANCE_NAME"
