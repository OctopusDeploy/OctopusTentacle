OctopusTentacle
---------------

### Notes

Issues related to fixes in this repo should be created in the Tentacle repo so they appear in the release notes.

Tentacle is released via ReleaseBot. Run `@releasebot What's up Octopus Tentacle` to get him to spring into action.

To include a new version into the next Octopus Server release, update the [reference in Octopus.E2ETests.csproj](https://github.com/OctopusDeploy/OctopusDeploy/blob/master/source/Octopus.E2ETests/Octopus.E2ETests.csproj#L29).

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
            "program": "${workspaceFolder}/source/Octopus.Tentacle/bin/netcoreapp3.1/Tentacle.dll",
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
- Make sure the build task (in `.vscode/tasks.json`) specifies the target framework, by inclusing `--framework=netcoreapp3.1` as a build arg, otherwise VSCode will attempt to build for all frameworks in the csproj and fail on full .Net framework. the build task should look similar to:
```
{
    "label": "build",
    "command": "dotnet",
    "type": "process",
    "args": [
        "build",
        "--framework=netcoreapp3.1",
        "${workspaceFolder}/source/Octopus.Tentacle/Octopus.Tentacle.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
    ],
    "problemMatcher": "$msCompile"
}
```