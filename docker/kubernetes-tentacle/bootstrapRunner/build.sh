#!/bin/bash

  # upx is a tool which reduces the size of the go executable.
  # unfortunately there is a not a good container for it and
  # it's not available to download via apt-get so we direct
  # download it.
if ${USE_UPX}; then
  TARGETARCH="$(dpkg --print-architecture)";
  TARGETOS="linux"

  apt update
  apt install -y xz-utils

  tarName="upx-${UPX_VERSION}-${TARGETARCH}_${TARGETOS}";
  fullTarName="$tarName.tar.xz"
  url="https://github.com/upx/upx/releases/download/v${UPX_VERSION}/$fullTarName";

  echo "Downloading upx from $url";
  curl -OL "$url";

  echo "Unpacking upx"
  tar -x -f "$fullTarName"
fi

for platform in ${PLATFORMS//,/$IFS}; do
  # ${platform%/*} removes the first string matching the regex /*
  # from the end of the string in the $platform (eg: /amd64 in 'linux/amd64')
  export GOOS=${platform%/*}

  # ${platform#*/} removes the first string matching the regex */
  # from the start of the string in $platform (eg: linux/ in 'linux/arm64')
  export GOARCH=${platform#*/}

  exeName="bootstrapRunner-$GOOS-$GOARCH"

  echo "Building BootstrapRunner for $platform"
  # the given ldflags remove debug symbols
  go build -ldflags "-s -w" -o "./bin/$exeName"

  if ${USE_UPX}; then
    echo "Compressing executable $exeName with upx"
    "./$tarName/upx" "./bin/$exeName"
  fi
done

if ${USE_UPX}; then
  echo "Cleaning up upx files"
  # cleanup upx files
  rm -rf "./$tarName"
  rm "./$fullTarName"
fi