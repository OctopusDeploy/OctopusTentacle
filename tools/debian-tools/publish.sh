#!/bin/bash

aptly repo create -distribution=stretch -component=main tentacle-release

aptly mirror create -ignore-signatures octopus-apt-mirror https://s3.amazonaws.com/octopus-apt-repo/ stretch

aptly mirror update -ignore-signatures octopus-apt-mirror

aptly repo import octopus-apt-mirror tentacle-release tentacle

aptly repo add tentacle-release /app

# aptly publish repo tentacle-release s3:octopus-apt-repo: