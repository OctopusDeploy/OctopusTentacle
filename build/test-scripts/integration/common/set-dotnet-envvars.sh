#!/bin/bash
set -o xtrace
set -e

# Use our paths rather than any system level dotnet
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH
