param($octopusServerThumbprint,
$octopusServerUrl,
$octopusServerApiKey,
$adminPassword)

$env:octopusServerThumbprint = $octopusServerThumbprint
$env:octopusServerUrl = $octopusServerUrl
$env:octopusServerApiKey = $octopusServerApiKey
$env:adminPassword = $adminPassword

$env:octopusServerRole = 'OsTestingListening'
$env:octopusServerEnvironment = 'OsTesting'
$env:tentacleVersion = '8.1.32'

## Takes this on to the end of the tentacle's name as seen in octopus
$env:tentacleNamePostfix = "$env:tentacleVersion"

$group = 'TentacleOldTimeout'
$resourcegroup = $group

az group create --name $group --location australiaeast

# .NET 6.0
#---------------------------------
$env:tentacleUri = "https://octopus-downloads-staging.s3.amazonaws.com/octopus/Octopus.Tentacle.$env:tentacleVersion-net6.0-win-x64.msi"
$netFramworkVersion = 'net6'

# Windows 2012
$env:virtualMachineName = 'Net6Tentacle-Window2012'
$env:virtualMachineSku = '2012-datacenter-gensecond'
$env:os = 'windows2012'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2012R2
$env:virtualMachineName = 'Net6Tentacle-Window2012R2'
$env:virtualMachineSku = '2012-r2-datacenter-gensecond'
$env:os = 'windows2012R2'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2016
$env:virtualMachineName = 'Net6Tentacle-Window2016'
$env:virtualMachineSku = '2016-datacenter-gensecond'
$env:os = 'windows2016'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2019
$env:virtualMachineName = 'Net6Tentacle-Window2019'
$env:virtualMachineSku = '2019-datacenter-gensecond'
$env:os = 'windows2019'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2022
$env:virtualMachineName = 'Net6Tentacle-Window2022'
$env:virtualMachineSku = '2022-datacenter-azure-edition'
$env:os = 'windows2022'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam


# .NET 4.8
#---------------------------------
$env:tentacleUri = "https://octopus-downloads-staging.s3.amazonaws.com/octopus/Octopus.Tentacle.$env:tentacleVersion-x64.msi"
$netFramworkVersion = 'net48'

# Windows 2012
$env:virtualMachineName = 'Net48Tentacle-Window2012'
$env:virtualMachineSku = '2012-datacenter-gensecond'
$env:os = 'windows2012-48'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2012R2
$env:virtualMachineName = 'Net48Tentacle-Window2012R2'
$env:virtualMachineSku = '2012-r2-datacenter-gensecond'
$env:os = 'windows2012R2-48'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2016
$env:virtualMachineName = 'Net48Tentacle-Window2016'
$env:virtualMachineSku = '2016-datacenter-gensecond'
$env:os = 'windows2016-48'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2019
$env:virtualMachineName = 'Net48Tentacle-Window2019'
$env:virtualMachineSku = '2019-datacenter-gensecond'
$env:os = 'windows2019-48'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

# Windows 2022
$env:virtualMachineName = 'Net48Tentacle-Window2022'
$env:virtualMachineSku = '2022-datacenter-azure-edition'
$env:os = 'windows2022-48'
az deployment group create --name $group --resource-group $resourcegroup --template-file vm.bicep --parameters vm.bicepparam

echo "Net48 tentacles wont connect, restart all net48 VMs then re-run the script in c:\\TentacleInstallRun.ps1 OR after restart modify the install-tentacle.ps1 script (add an extra Write-Host) so bicep see's it as a differnt script and so will re-run the script on the VMs again."