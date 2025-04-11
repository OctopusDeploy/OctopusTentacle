# Build and Release - What Happens Following A Pull Request

When a pull request is created, the build process is initiated by TeamCity. 

## TeamCity

TeamCity is responsible for producing `.nupkg` files, which are artifact packages. These artifacts are stored in [Artifactory](https://packages.octopushq.com/ui/repos/tree/General/chocolatey). For every PR, a `.nupkg` file is created.

Following this, TeamCity creates a release in [Octopus Deploy](https://deploy.octopus.app/app#/Spaces-1/projects/octopus-tentacle/deployments?groupBy=Channel), preparing it for deployment.

## Octopus Deploy

Octopus Deploy performs three key actions:

1. **Copying Installer Files**: The tentacle installer files for various platforms are copied to S3 storage. 

2. **Creating a Release on Slipway**: Octopus Deploy calls the Slipway API to create a release on Slipway. Slipway determines if the change is significant and decides whether to publish it on the [Downloads page](https://octopus.com/downloads/tentacle). 

3. **Registering with Chocolatey**: Octopus Deploy executes the Chocolatey command to register the new version with Chocolatey. This ensures that when a user runs `choco install octopusdeploy.tentacle.selfcontained`, the `ChocolateyInstall.ps1` script is executed.

Additionally, our URL shortener Short.io, points to a Slipway endpoint, which serves the latest Tentacle version. 

## Slipway

Regardless of whether a change is meaningful or not, all releases are visible [here](https://slipway.octopushq.com/software-products/OctopusTentacle/releases). Slipway uses pattern matching to differentiate between the two .NET flavours of Tentacle.
