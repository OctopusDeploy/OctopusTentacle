export VERSION=$(get_octopusvariable "Octopus.Action.Package[Tentacle.Packages.linux].PackageVersion")
export AWS_ACCESS_KEY_ID=$(get_octopusvariable "Publish.APT.S3.AccessKeyId")
export AWS_SECRET_ACCESS_KEY=$(get_octopusvariable "Publish.APT.S3.SecretAccessKey")
export GPG_PASSPHRASE=$(get_octopusvariable "Publish.APT.GPG.PassPhrase")
export GPG_PRIVATEKEYFILE="octopus-privatekey.asc"
S3_PUBLISH_ENDPOINT=$(get_octopusvariable "Publish.APT.S3.TargetBucket")
GPG_PRIVATEKEY=$(get_octopusvariable "Publish.APT.GPG.PrivateKey")
echo "$GPG_PRIVATEKEY" > $GPG_PRIVATEKEYFILE

cp .aptly.conf ~/.aptly.conf

echo "Importing private key: $GPG_PRIVATEKEYFILE with passphrase: $GPG_PASSPHRASE"

echo $GPG_PASSPHRASE | gpg1 --batch --import $GPG_PRIVATEKEYFILE
wget -O - https://s3.amazonaws.com/$S3_PUBLISH_ENDPOINT/public.key | gpg1 --no-default-keyring --keyring trustedkeys.gpg --import

echo "Configuring S3 bucket"
aws s3 mb "s3://$S3_PUBLISH_ENDPOINT"
aws s3api wait bucket-exists --bucket $S3_PUBLISH_ENDPOINT
aws s3 sync ./apt-content "s3://$S3_PUBLISH_ENDPOINT" --acl public-read

echo "Updating APT repo"
aptly repo create -distribution=stretch -component=main octopus-tentacle

aptly mirror create octopus-apt-mirror https://s3.amazonaws.com/$S3_PUBLISH_ENDPOINT/ stretch
aptly mirror update octopus-apt-mirror

aptly repo import octopus-apt-mirror octopus-tentacle tentacle
aptly repo add octopus-tentacle ./Tentacle.Packages.linux

aptly repo show -with-packages octopus-tentacle

aptly publish repo -batch -passphrase="$GPG_PASSPHRASE" octopus-tentacle s3:$S3_PUBLISH_ENDPOINT: