# Azure Infrastructure for Compatibility Testing

## Setup (Windows)

Optional - Install the [VsCode Extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-bicep)

Install the Azure CLI

> winget install -e --id Microsoft.AzureCLI

Install Bicep

> az bicep install

## Deploying

> az login

> az account set --subscription {subscriptionId}

> ./deploy.ps1 -octopusServerThumbprint xyz -octopusServerUrl http://abc.octopus.app -octopusServerApiKey 123 -adminPassword qwerty123!@#

## Deleting

THIS IS A DESTRUCTIVE OPERATION 

> az group delete --name Tentacle 