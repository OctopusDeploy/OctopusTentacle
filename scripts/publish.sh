#!/bin/bash

echo $GPG_PASSPHRASE | gpg1 --batch --import /certs/$GPG_PRIVATEKEYFILE
wget -O - https://s3.amazonaws.com/octopus-apt-repo/public.key | gpg1 --no-default-keyring --keyring trustedkeys.gpg --import

aptly repo create -distribution=stretch -component=main octopus-tentacle

aptly mirror create octopus-apt-mirror https://s3.amazonaws.com/octopus-apt-repo/ stretch
aptly mirror update octopus-apt-mirror

aptly repo import octopus-apt-mirror octopus-tentacle tentacle
aptly repo add octopus-tentacle /app

aptly repo show -with-packages octopus-tentacle

aptly publish repo -passphrase="$GPG_PASSPHRASE" octopus-tentacle s3:octopus-apt-repo:
