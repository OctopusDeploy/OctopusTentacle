# Azure Infrastructure for Compatibility Testing

## Setup (Windows)

Optional - Install the [VsCode Extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-bicep)

Install the Azure CLI

> winget install -e --id Microsoft.AzureCLI

Install Bicep

> az bicep install

## Deploying


### Login
> az login

It may not install the correct tenant (subscription). In which case, do this:

> az login -t {Tentant Domain}
For example:
> az login -t octopusdeploysandbox.onmicrosoft.com

Choose the appropriate subsription. 
E.g. In Octopus Sandbox, Subscriptions -> Team Core Platform - Sandbox (c772d4b7-5ac7-4e9c-a259-9f84743d4ae5)

> az account set --subscription {Subscription Id}
For example, for Team Core Platform - Sandbox:
> az account set --subscription c772d4b7-5ac7-4e9c-a259-9f84743d4ae5


### Change Scripts
Ensure the variables in `deploy.ps1` are correct. Some variables of note:
- octopusServerRole - The role that the Tentacles will be tagged with.
- octopusServerEnvironment - The environment the Tentacles will be registered against.
- tentacleVersion - The version of the Tentacle being used.

### Deploy
Now, install the VMs and install Tentacle:
> ./deploy.ps1 -octopusServerThumbprint {OctopusServerThumbprint} -octopusServerUrl {OctopusServerUri} -octopusServerApiKey {OctopusServerApiKey} -adminPassword qwerty123!@#
Where:
- OctopusServerUri - The URI of the instance you wish to register against. E.g. https://sast-rpc-retires.testoctopus.app
- OctopusServerThumbprint - The thumbprint from the same Octopus Server (found in Settings => Thumbprint)
- OctopusServerApiKey - The API key to use from the same Octopus Server (create via your profile on the Octopus Server instance)

This script will take a while. 

There may be limitations reached in Azure. Be sure to increase these limitations, or work around them by choosing other resources. E.g. choosing the better `hardwareProfile.vmSize: 'Standard_D2as_v5'` in `vm.bicep`.

You can view progress via Azure itself via the Resource Group for the subscription used above.

Ensure that it installs the tentacles within the instance of Octopus Server chosen above.

Note that for the .NET 4.8 instances, some will require a restart of the VM. This can be done by RDPing into them via Azure, and restarting.



## Deleting

THIS IS A DESTRUCTIVE OPERATION 

> az group delete --name Tentacle 