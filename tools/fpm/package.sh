#!/bin/bash

fpm -n tentacle-service -s pleaserun -t dir /opt/octopus/tentacle/Tentacle agent

fpm -n tentacle -s dir -t deb --deb-no-default-config-files --after-install setup.sh --before-remove uninstall.sh /app/publish/=/opt/octopus/tentacle ./tentacle-service.dir/usr/share/pleaserun/=/usr/share/pleaserun

rm -rf tentacle-service.dir

cp tentacle_1.0_amd64.deb /app