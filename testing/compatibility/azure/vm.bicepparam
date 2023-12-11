using 'vm.bicep'

param location = toLower('australiaeast')
param virtualMachineName = readEnvironmentVariable('virtualMachineName', '')
param virtualMachineSku = readEnvironmentVariable('virtualMachineSku', '')
param adminUsername = 'tentacle'
param adminPassword = readEnvironmentVariable('adminPassword', '')

param octopusServerThumbprint = readEnvironmentVariable('octopusServerThumbprint', '')
param octopusServerUrl = readEnvironmentVariable('octopusServerUrl', '')
param octopusServerApiKey = readEnvironmentVariable('octopusServerApiKey', '')
param octopusServerRole = readEnvironmentVariable('octopusServerRole', '')
param octopusServerEnvironment = readEnvironmentVariable('octopusServerEnvironment', '')
param os = readEnvironmentVariable('os', '')
param tentacleUri = readEnvironmentVariable('tentacleUri', '')
param tentacleNamePostfix = readEnvironmentVariable('tentacleNamePostfix', '')
