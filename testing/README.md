# Manually Testing Tentacle

## Compatibility Testing

The following Vagrant and Docker and Azure scripts are available to provision Tentacles and assist with manually setting up different versions of Tentacle for compatibility testing.

### Vagrant

Use the [Vagrant](./compatibility/Vagrant/README.md) scripts to setup a Windows or Linux VM with a selection of Tentacle versions, both Polling and Listening Deployment Targets and Workers are supported.

### Docker

Use the [Docker Compose](./compatibility/docker/README.md) scripts to setup docker containers with a selection of Tentacle versions, both Polling and Listening Deployment Targets and Workers are supported.

### Azure

Use the [Azure](./compatibility/azure/README.md) scripts to setup Azure Virtual Machines with a selection of Tentacle versions, both Polling and Listening Deployment Targets and Workers are supported.

## Linux Docker Image

Use [`docker-linux/build-and-test-linux-docker-image.sh`](./docker-linux/build-and-test-linux-docker-image.sh) to build and verify the Linux Tentacle container image (`docker/linux/Dockerfile`) end to end, the way TeamCity does. This is the only local way to check a change to that Dockerfile, since the image is built in TeamCity rather than by NUKE.

The script runs in four stages, each of which can be skipped:

| Stage | What it does |
|---|---|
| `deb` | Builds the linux-x64 `.deb` via NUKE (`--target PackDebianPackage`) |
| `image` | Builds the image with the same `docker-compose.build.yml` command TeamCity runs |
| `smoke` | Asserts the image's contents and behaviour - base image, .NET runtime dependencies, docker-in-docker, entrypoint guards, labels |
| `e2e` | Stands up SQL Server and an Octopus Server, then registers a listening and a polling Tentacle built from the fresh image |

```
# everything
./docker-linux/build-and-test-linux-docker-image.sh

# reuse the last .deb and image, just re-run the assertions
./docker-linux/build-and-test-linux-docker-image.sh --skip-deb --skip-image

# full options
./docker-linux/build-and-test-linux-docker-image.sh --help
```

The `e2e` stage needs an Octopus Server licence, because an unlicensed server enforces a limit of 0 targets and no Tentacle can register. Set `OCTOPUS_SERVER_BASE64_LICENSE`, or let the script read a development licence from 1Password when run interactively. Without one, the stage still verifies configuration and server connectivity up to the licence refusal. Pass `--no-1password` anywhere `op` must not run.
