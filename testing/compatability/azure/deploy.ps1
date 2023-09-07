param($octopusServerThumbprint,
$octopusServerUrl,
$octopusServerApiKey,
$adminPassword)

$env:octopusServerThumbprint = $octopusServerThumbprint
$env:octopusServerUrl = $octopusServerUrl
$env:octopusServerApiKey = $octopusServerApiKey
$env:adminPassword = $adminPassword

$env:octopusServerRole = 'OsTestingTentacle'
$env:octopusServerEnvironment = 'OsTesting'
$env:tentacleVersion = '7.0.201'

az group create --name Tentacle --location australiaeast

# Windows 2012
$env:virtualMachineName = 'Net6Tentacle-Window2012'
$env:virtualMachineSku = '2012-datacenter-gensecond'
$env:os = 'windows2012'
az deployment group create --name Tentacle --resource-group Tentacle --template-file vm.bicep --parameters vm.bicepparam

# Windows 2012R2
$env:virtualMachineName = 'Net6Tentacle-Window2012R2'
$env:virtualMachineSku = '2012-r2-datacenter-gensecond'
$env:os = 'windows2012R2'
az deployment group create --name Tentacle --resource-group Tentacle --template-file vm.bicep --parameters vm.bicepparam

# Windows 2016
$env:virtualMachineName = 'Net6Tentacle-Window2016'
$env:virtualMachineSku = '2016-datacenter-gensecond'
$env:os = 'windows2016'
az deployment group create --name Tentacle --resource-group Tentacle --template-file vm.bicep --parameters vm.bicepparam

# Windows 2019
$env:virtualMachineName = 'Net6Tentacle-Window2019'
$env:virtualMachineSku = '2019-datacenter-gensecond'
$env:os = 'windows2019'
az deployment group create --name Tentacle --resource-group Tentacle --template-file vm.bicep --parameters vm.bicepparam

# Windows 2022
$env:virtualMachineName = 'Net6Tentacle-Window2022'
$env:virtualMachineSku = '2022-datacenter-azure-edition'
$env:os = 'windows2022'
az deployment group create --name Tentacle --resource-group Tentacle --template-file vm.bicep --parameters vm.bicepparam