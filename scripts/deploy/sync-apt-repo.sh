export VERSION=$(get_octopusvariable "Octopus.Action.Package[Tentacle.Packages.linux].PackageVersion")
export AWS_ACCESS_KEY_ID=$(get_octopusvariable "S3:ACCESS_KEY_ID")
export AWS_SECRET_ACCESS_KEY=$(get_octopusvariable "S3:SECRET_ACCESS_KEY")
export GPG_PASSPHRASE=$(get_octopusvariable "GPG_PASSPHRASE")
export GPG_PRIVATEKEYFILE="octopus-privatekey.asc"
S3_PUBLISH_ENDPOINT=$(get_octopusvariable "S3PublishEndpoint")
GPG_PRIVATEKEY=$(get_octopusvariable "GPG_PRIVATEKEY")
echo "$GPG_PRIVATEKEY" > $GPG_PRIVATEKEYFILE

cp .aptly.conf ~/.aptly.conf

echo "importing private key: $GPG_PRIVATEKEYFILE"
echo "with passphrase: $GPG_PASSPHRASE"

echo $GPG_PASSPHRASE | gpg1 --batch --import $GPG_PRIVATEKEYFILE
wget -O - https://s3.amazonaws.com/$S3_PUBLISH_ENDPOINT/public.key | gpg1 --no-default-keyring --keyring trustedkeys.gpg --import

aptly repo create -distribution=stretch -component=main octopus-tentacle

aptly mirror create octopus-apt-mirror https://s3.amazonaws.com/$S3_PUBLISH_ENDPOINT/ stretch
aptly mirror update octopus-apt-mirror

aptly repo import octopus-apt-mirror octopus-tentacle tentacle
aptly repo add octopus-tentacle ./Tentacle.Packages.linux

aptly repo show -with-packages octopus-tentacle

aptly publish repo -batch -passphrase="$GPG_PASSPHRASE" octopus-tentacle s3:$S3_PUBLISH_ENDPOINT: