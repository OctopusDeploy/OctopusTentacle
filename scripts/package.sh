#!/bin/bash

fpm -v $VERSION \
  -n tentacle-service \
  -s pleaserun \
  -t dir \
  /opt/octopus/tentacle/Tentacle agent --noninteractive

# Dependencies based on https://github.com/dotnet/dotnet-docker/blob/master/2.1/runtime-deps/bionic/amd64/Dockerfile
fpm -v $VERSION \
  -n tentacle \
  -s dir \
  -t deb \
  -m '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Octopus Tentacle package' \
  -d 'libc6' \
  -d 'libgcc1' \
  -d 'libgssapi-krb5-2' \
  -d 'libicu60' \
  -d 'liblttng-ust0' \
  -d 'libssl1.0.0' \
  -d 'libstdc++6' \
  -d 'zlib1g' \
  --deb-no-default-config-files \
  --after-install setup.sh \
  --before-remove uninstall.sh \
  $TENTACLE_BINARIES=/opt/octopus/tentacle \
  ./tentacle-service.dir/usr/share/pleaserun/=/usr/share/pleaserun

fpm -v $VERSION \
  -n tentacle \
  -s dir \
  -t rpm \
  -m '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Octopus Tentacle package' \
  -d 'lttng-ust' \
  -d 'libcurl' \
  -d 'openssl-libs' \
  -d 'krb5-libs' \
  -d 'libicu' \
  -d 'zlib' \
  --after-install setup.sh \
  --before-remove uninstall.sh \
  $TENTACLE_BINARIES=/opt/octopus/tentacle \
  ./tentacle-service.dir/usr/share/pleaserun/=/usr/share/pleaserun

rm -rf tentacle-service.dir

mkdir -p $ARTIFACTS

cp -f *.{deb,rpm} $ARTIFACTS
