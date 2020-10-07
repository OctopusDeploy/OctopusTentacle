#!/usr/bin/env bash
set -eux

dotnet tool restore
dotnet tool run dotnet-cake --bootstrap --verbosity=Diagnostic
dotnet tool run dotnet-cake $@
