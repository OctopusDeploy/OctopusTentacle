# Docker

Setup docker containers with a set of Polling and Listening Deployment Targets and Workers

## Pre-Req Installation

Install Powershell

Install Docker

## Configuration

Copy the file `example.config.json` to `config.json` and modify the settings

- **pollingDeploymentTargets** true to set-up polling deployment targets e.g. `"pollingDeploymentTargets": true`
- **listeningDeploymentTargets** true to set-up listening deployment targets e.g. `"listeningDeploymentTargets": true`
- **pollingWorkers** true to set-up polling workers e.g. `"pollingWorkers": true`
- **listeningWorkers** true to set-up listening workers e.g. `"listeningWorkers": true`
- **tentacleVersions** comma separated list of versions to install e.g. `"tentacleVersions": "6.1.1531,6.2.277,6.3.417,6.3.605,latest"`

Copy the file `example.env` to `.env` and modify the settings

- **serverUrl** The url of Octopus Server to configure the Tentacles for e.g. `serverUrl=http://mymachine.local:8066`
- **serverPollingPort** The Octopus Server polling port e.g. `serverPollingPort=10943`
- **serverApiKey** The Octopus Server API Key e.g. `serverApiKey=API-APIKEY01`
- **environment** The environment to configure the Deployment Targets for e.g. `environment=Development`
- **pollingRole** The role to assign to the polling Deployment Targets e.g. `pollingRole=pollingBreadRoll`
- **listeningRole** The role to assign to the listening Deployment Targets e.g. `listeningRole=listeningBreadRoll`
- **space** the space to configure the Tentacle for e.g. `space=Default` Note: Versions of Tentacle prior to `4.x` had no concept of spaces so will be configured against the `Default` workspace
- **workerPool** The name of the worker pool to add workers to e.g. `workerPool=Default Worker Pool`

`tentacleVersions` can be any version that is available on [Docker Hub](https://hub.docker.com/r/octopusdeploy/tentacle/tags)

## Running

Generate the docker compose file

`.\generate.ps1`

> This will generate 2 files. `config.generated.json` and `docker-compose.yml`

Deploy the containers

`.\run.ps1`

> This calls `docker-compose up --remove-orphans`

Delete the containers

`docker-compose down`