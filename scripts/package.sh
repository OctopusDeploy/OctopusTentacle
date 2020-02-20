#!/bin/bash

# Remove existing packages, fpm doesnt like to overwrite
rm *.{deb,rpm}

fpm --version $VERSION \
  --name tentacle \
  --input-type dir \
  --output-type deb \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Octopus Tentacle package' \
  --deb-no-default-config-files \
  --after-install setup.sh \
  --before-remove uninstall.sh \
  $TENTACLE_BINARIES=/opt/octopus/tentacle

fpm --version $VERSION \
  --name tentacle \
  --input-type dir \
  --output-type rpm \
  --maintainer '<support@octopus.com>' \
  --vendor 'Octopus Deploy' \
  --url 'https://octopus.com/' \
  --description 'Octopus Tentacle package' \
  --after-install setup.sh \
  --before-remove uninstall.sh \
  $TENTACLE_BINARIES=/opt/octopus/tentacle

mkdir tentacle

cp -a $TENTACLE_BINARIES/. tentacle/

tar -czvf tentacle-$VERSION-linux_x64.tar.gz tentacle

mkdir -p $ARTIFACTS

cp -f *.tar.gz $ARTIFACTS

cp -f *.{deb,rpm} $ARTIFACTS
