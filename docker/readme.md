This image can be used to bring up an [Octopus Tentacle in a container](https://octopus.com/docs/installation/octopus-tentacle-container).

# Pre-Requisites

Docker containers are supported on Windows Server 2016 or later and Windows 10 or later. 

Make sure you've enabled the containers feature:

```
Enable-WindowsOptionalFeature -Online -FeatureName containers –All
```

If you want to run with [Hyper-V isolation](https://docs.microsoft.com/en-us/virtualization/windowscontainers/manage-containers/hyperv-container), enable Hyper-V as well:

```
Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V –All
```

You will also need [Docker for Windows](https://www.docker.com/community-edition#/windows) installed.

# Notes #
On Linux containers, prior to version `6.1.1271` the internal listening port was set by the `ListeningPort` environment variable. Any containers which previously exposed Tentacle on a port other than `10933` will need to have their port configuration updated if updating to a version `>=6.1.1271`. For example if the container was run with `-p 10934:10934` this should be updated to `-p 10934:10933`.

# Usage #

On a Windows Server 2016 server, or on Windows 10, run:

```
docker run --publish 10931:10933 `
           --tty --interactive `
           --env ListeningPort="10931" `
           --env ServerApiKey="API-WZ27UDXXAPCKUPZSH1WTG8YC80G" `
           --env TargetEnvironment="Test" `
           --env TargetRole="app-server" `
           --env ServerUrl="https://octopus.example.com" `
           --env PublicHostNameConfiguration="ComputerName" `
           --env ACCEPT_EULA="Y" `
           octopusdeploy/tentacle
```


It is recommended that you run this using something like docker compose, so that it sets up and handles networking for you. Please see the [`docker-compose.yml` file in the Octopus Tentacle](https://github.com/OctopusDeploy/OctopusTentacle/blob/main/docker-compose.yml) repo for an example. Otherwise, use `docker network` so that the containers can talk to each other.

### Environment variables

- **ACCEPT_EULA**: Set to Y to indicate that you accept the EULA.
- **DISABLE_DIND**: Set to Y to disable Docker-in-Docker (used to run container images).
- **ServerApiKey**: The API Key of the Octopus Server the Tentacle should register with.
- **ServerUsername**: If not using an API key, the user to use when registering the Tentacle with the Octopus Server.
- **ServerPassword**: If not using an API key, the password to use when registering the Tentacle.
- **ServerUrl**: The Url of the Octopus Server the Tentacle should register with.
- **Space**: The name of the space which the Tentacle will be added to. Defaults to the default space.
- **TargetEnvironment**: Comma delimited list of environments to add this target to.
- **TargetRole**: Comma delimited list of roles to add to this target.
- **TargetWorkerPool**: Comma delimited list of worker pools to add to this target to (not to be used with environments or role variable).
- **TargetName**: Optional Target name, defaults to host.
- **TargetTenant**: Comma delimited list of tenants to add to this target.
- **TargetTenantTag**: Comma delimited list of tenant tags to add to this target.
- **TargetTenantedDeploymentParticipation**: The tenanted deployment mode of the target. Allowed values are Untenanted, TenantedOrUntenanted and Tenanted. Defaults to Untenanted.
- **MachinePolicy**: The name of the machine policy that will apply to this Tentacle. Defaults to the default machine policy.
- **ServerCommsAddress**: The URL of the Octopus Server that the Tentacle will poll for work. Defaults to `ServerUrl`. Implies a polling Tentacle.
- **ServerPort**: The port on the Octopus Server that the Tentacle will poll for work. Defaults to 10943. Implies a polling Tentacle.
- **ListeningPort**: The port that the Octopus Server will connect back to the Tentacle with. Defaults to 10933. Implies a listening Tentacle.
- **PublicHostNameConfiguration**: How the url that the Octopus server will use to communicate with the Tentacle is determined. Can be `PublicIp`, `FQDN`, `ComputerName` or `Custom`. Defaults to `PublicIp`.
- **CustomPublicHostName**: If `PublicHostNameConfiguration` is set to `Custom`, the host name that the Octopus Server should use to communicate with the Tentacle.


### Ports

- **10933**: Port Tentacle will be listening on.

## Support ##

Please contact [Octopus Support](https://octopus.com/support) for support.

## Additional Information ##

* These images are based off the [OctopusTentacle](https://github.com/OctopusDeploy/OctopusTentacle) repo on GitHub.
