#!/bin/bash

# Remove existing packages, fpm doesnt like to overwrite
rm *.{deb,rpm}

fpm -v $VERSION \
  -n tentacle-service \
  -s pleaserun \
  -t dir \
  /opt/octopus/tentacle/Tentacle run --noninteractive

# Dependencies based on https://github.com/dotnet/dotnet-docker/blob/master/2.1/runtime-deps/bionic/amd64/Dockerfile
fpm -v $VERSION \
  -n tentacle \
  -s dir \
  -t deb \
  -m '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Octopus Tentacle package' \
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
  --after-install setup.sh \
  --before-remove uninstall.sh \
  $TENTACLE_BINARIES=/opt/octopus/tentacle \
  ./tentacle-service.dir/usr/share/pleaserun/=/usr/share/pleaserun

rm -rf tentacle-service.dir

mkdir tentacle

cp -a $TENTACLE_BINARIES/. tentacle/

tar -czvf tentacle-$VERSION-linux_x64.tar.gz tentacle

mkdir -p $ARTIFACTS

cp -f *.tar.gz $ARTIFACTS

cp -f *.{deb,rpm} $ARTIFACTS
