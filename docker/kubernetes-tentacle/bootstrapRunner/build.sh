#!/bin/bash

curl -o ./upx.tar.xz "https://github.com/upx/upx/releases/download/v${UPX_VERSION}/upx-${UPX_VERSION}-${TARGETARCH}_${TARGETOS}.tar.xz"
tar -xv -f ./upx.tar.xz

for platform in ${GOPLATFORMS//,/$IFS}; do
    parts=${platform//\//$IFS}
    export GOARCH
    go build -v -o bootstrapRunner-linux-$GOARCH
    ./upx/upx bootstrapRunner-linux-$GOARCH
done