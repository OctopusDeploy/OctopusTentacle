# Tentacle

Tentacle is the secure, lightweight, cross-platform agent for [Octopus Server](https://github.com/OctopusDeploy/OctopusDeploy) which turns any computer into a worker or deployment target for automated deployments and operations runbooks.

![Tentacles Everywhere](https://user-images.githubusercontent.com/1627582/92418318-430ed000-f1aa-11ea-8a46-6d6763feef3a.png)

## Code of Conduct

This project and everyone participating in it is governed by the [Octopus Deploy Code of Conduct](https://github.com/OctopusDeploy/.github/blob/main/CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior using the instructions in the code of conduct.

## Did you find a bug?

If the bug is a security vulnerability in Octopus Deploy, please refer to our [security policy](https://github.com/OctopusDeploy/.github/blob/main/SECURITY.md).

Search our [public Issues repository](https://github.com/OctopusDeploy/Issues) to ensure the bug was not already reported.

If you're unable to find an open issue addressing the problem, please follow our [support guidelines](https://github.com/OctopusDeploy/.github/blob/main/SUPPORT.md).

## Contributing

### Issues, Pull Requests, and Release Notes

:+1::tada: First off, thanks for your contribution to Tentacle! :tada::+1:

Please [create a new issue](https://github.com/OctopusDeploy/OctopusTentacle/issues/new) for each bug fix or enhancement. [Issues in this repository](https://github.com/OctopusDeploy/OctopusTentacle/issues) are automatically used to create release notes for [releases](https://github.com/OctopusDeploy/OctopusTentacle/releases).

Please ensure every commit that hits `main` links either to an issue directly, or to a PR that in turn links to an issue. Please use the [appropriate keywords to close issues](https://docs.github.com/en/issues/tracking-your-work-with-issues/linking-a-pull-request-to-an-issue#linking-a-pull-request-to-an-issue-using-a-keyword). Doing so will ensure that your changes are available for users to download via the [Downloads](https://octopus.com/downloads/tentacle) page.

If you don't use a keyword, even if your changes are merged into main and a new version created, it will not appear on the Downloads page.

Please see [How to Raise a Meaningful Change](docs/meaningful-change.md) for a step-by-step guide.

### Build, Test, and Delivery

We use the [Octopus Tentacle project in our private TeamCity server](https://build.octopushq.com/project/OctopusDeploy_OctopusTentacle) for automated build and test reporting status changes to pull requests.

We use the [Octopus Tentacle project in our private Octopus server](https://deploy.octopus.app/app#/Spaces-1/projects/octopus-tentacle) to deploy releases of Tentacle.

Deployments happen automatically - a merge to `main` will trigger a build and a deployment - continuous delivery for the win!

For internal developers, on closing an issue, ReleaseBot will ask you for release notes.
For external developers, or if ReleaseBot fails for some reason, please add a comment to the issue `Release note: XXXX` to ensure release notes are generated correctly.

An easy way to find the code associated with a particular version of Tentacle is to check out the [tags](https://github.com/OctopusDeploy/OctopusTentacle/tags). 

#### Incrementing Major Versions

To increment a major version, make an empty commit with the commit message `+semver: major`. This is a special instruction to GitVersion.

```
git commit --allow-empty -m "+semver: major"
```

For further details refer to [Tentacle Rollout](docs/rollout.md).

### Bundling Tentacle with Octopus Server

We bundle Tentacle inside Octopus Server to make it super duper easy to keep Tentacle updated across entire fleets of customer installations. Choosing the version of Tentacle to bundle inside Octopus Server is currently a manual process.

To include a new version into the next Octopus Server release, update the [reference in Octopus.Server.csproj](https://github.com/OctopusDeploy/OctopusDeploy/blob/master/source/Octopus.Server/Octopus.Server.csproj#L36). This is how we guarantee the version of Tentacle we bundle is also the version we use for all the end to end tests.

## Debugging

Generally the simplest way to debug the Tentacle codebase is to install a Tentacle like a customer would (a Tentacle "instance"), connect it to Octopus Server and then use this instance when debugging in your IDE.

### 1. Set up a Tentacle and connect it to Octopus Server

There are multiple ways to install and connect a Tentacle to Octopus Server, the best place to get started with this is the [Octopus Tentacle documentation](https://octopus.com/docs/infrastructure/deployment-targets/tentacle).

Once the Tentacle is installed and connected to Octopus Server, take note of the name you gave the Tentacle during installation, this will be the "instance name" which will be used to debug the Tentacle codebase.

### 2. Stop the existing Tentacle service

Depending on how you installed Tentacle above, you may have a service installed (this could be a Windows service or systemd). You'll need to stop this so that it doesn't clash with any debugging you want to do.

### 3. Debug Tentacle in your IDE

When Tentacle runs it can read configuration from an installed instance in order to connect to Octopus Server, in this case this configuration is what we set up above for the installed Tentacle. 

To debug open up the Tentacle codebase in your IDE of choice and run the debugger with the following arguments:

`run --instance=INSTANCE_NAME` where `INSTANCE_NAME` is the name of the Tentacle from point 1.

When the debugger starts it will now be acting as your installed Tentacle and you can debug as required.

If you need to set up Tentacle, Halibut and Octopus Server for local development, refer to the [Local Development chain docs](https://github.com/OctopusDeploy/Halibut/blob/152535a0a8052ddf85c4a8f9b11375d0adc6fe3b/docs/local-build-chain.md) in the Halibut repository.

## Debugging in WSL (Windows Subsystem for Linux )

Currently we can only debug netcore apps running in WSL from VSCode, Visual Studio and Rider dont seem to have good working solutions

- Install VSCode
- Install Remote Dev pack for VSCode https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.vscode-remote-extensionpack
- Open a WSL VSCode session https://code.visualstudio.com/docs/remote/wsl#_getting-started
- Install C# extension, Even if this extension is already installed, this will need to be done within a WSL session as the extension binaries are platform specific.
- Open the Tentacle repository folder in VSCode and create a debug profile (https://code.visualstudio.com/docs/editor/debugging) example `.vscode/launch.json` file:

```
{
    // Use IntelliSense to learn about possible attributes.
    // Hover to view descriptions of existing attributes.
    // For more information, visit: https://go.microsoft.com/fwlink/?linkid=830387
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Tentacle Run command",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/source/Octopus.Tentacle/bin/net8.0/Tentacle.dll",
            "args": ["run"],
            "cwd": "${workspaceFolder}/source/Octopus.Tentacle",
            "console": "internalConsole",
            "stopAtEntry": false
        },
        {
            "name": ".NET Core Attach",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:pickProcess}"
        }
    ]
}
```

- Make sure the build task (in `.vscode/tasks.json`) specifies the target framework, by including `--framework=net8.0` as a build arg, otherwise VSCode will attempt to build for all frameworks in the csproj and fail on full .Net framework. the build task should look similar to:

```
{
    "label": "build",
    "command": "dotnet",
    "type": "process",
    "args": [
        "build",
        "--framework=net8.0",
        "${workspaceFolder}/source/Octopus.Tentacle/Octopus.Tentacle.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
    ],
    "problemMatcher": "$msCompile"
}
```

## Debugging the Kubernetes Agent Tentacle

The Kubernetes Agent Tentacle is more complex to debug, as it normally runs inside a Kubernetes Pod. To debug it locally, you can run the `setup-k8s-agent-for-local-debug.sh` script in the root of this repo which will guide you through the process of installing a specially configured kind cluster, deploying the agent to it and then scaling back the installed agent so you can run a local copy to take it's place.

NOTE: This script has only been tested on MacOS so far and requires Docker Desktop, Kubectl, Go CLI and Kind to be installed. It is also only for the Kubernetes Agent Tentacle running as a Deployment Target, not as a Worker.

## Additional Resources

- Scripts to help with manual testing can be found in [./testing](./testing/README.md).
