# Vagrant

Setup a Windows or Linux VM with a set of Polling and Listening Deployment Targets and Workers

> NOTE: For Windows host all commands should be run in the `./Windows` directory

> NOTE: For Linux host all commands should be run in the `./Linux` directory

## Install VirtualBox

Install [VirtualBox](https://www.virtualbox.org/wiki/Downloads)

> Hyper-V can be used on Windows but issues were experienced with Ubuntu networking and local DNS resolution.

## Install Vagrant

### Windows

Install Vagrant

`choco install vagrant -y`

See [Install Vagrant](https://developer.hashicorp.com/vagrant/downloads) for alternative installation options

### Linux

See [Install Vagrant](https://developer.hashicorp.com/vagrant/downloads) 

## Install Vagrant Plugins

`vagrant plugin install vagrant-env`

## Configuration

In the `./Windows` or `./Linux` directory Copy the file `example.env` to `.env` and modify the settings

- **serverUrl** The url of Octopus Server to configure the Tentacles for e.g. `serverUrl=http://mymachine:8066`
- **serverThumbprint** The Octopus Server Thumbprint e.g. `serverThumbprint=ABCDEF123456`
- **serverPollingPort** The Octopus Server polling port e.g. `serverPollingPort=10943`
- **serverApiKey** The Octopus Server API Key e.g. `serverApiKey=API-APIKEY01`
- **environment** The environment to configure the Deployment Targets for e.g. `environment=Development`
- **pollingRole** The role to assign to the polling Deployment Targets e.g. `pollingRole=pollingBreadRoll`
- **listeningRole** The role to assign to the listening Deployment Targets e.g. `listeningRole=listeningBreadRoll`
- **space** the space to configure the Tentacle for e.g. `space=Default` Note: Versions of Tentacle prior to `4.x` had no concept of spaces so will be configured against the `Default` workspace
- **tentacleVersions** comma separated list of versions to install e.g. `tentacleVersions=3.25.0,4.0.7,5.0.15,6.0.645,6.1.1531,6.2.277,6.3.417,6.3.605`
- **polling** True to set-up polling Tentacles e.g. `polling=True`
- **listening** True to set-up listening Tentacles e.g. `listening=True`
- **deploymentTargets** True to set-up deployment targets e.g. `deploymentTargets=True`
- **workers** True to set-up workers e.g. `workers=True`
- **workerPool** The name of the worker pool to add workers to e.g. `workerPool=Default Worker Pool`

`tentacleVersions` can be any version that is available on [Chocolatey](https://community.chocolatey.org/packages/OctopusDeploy.Tentacle#versionhistory) for Windows or [apt-get](https://octopus.com/docs/infrastructure/deployment-targets/tentacle/linux#installing-and-configuring-linux-tentacle) for Linux `apt-cache policy tentacle`

## Running

Create the VM and install the Tentacles with

`vargrant up`

> This can take 10+ minutes to create and provision the VM. More if you have a large number of Tentacle versions.

Delete the VM with

`vargant destroy`

Re-run the provisioning scripts (when the VM is already created and running) with

`vagrant provision`

> It is safe to run `vagrant up` followed by `vagrant provision` however not all changes to `.env` parameters may be supported e.g. removing a `tentacleVersion` will not delete the removed version and deregister it with Server but adding a new version will add it successfully.

> Issues were intermittently experience with mismatching certificates after running run `vagrant up` followed by `vagrant provision`