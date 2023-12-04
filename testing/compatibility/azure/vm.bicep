param location string
param virtualMachineName string
param adminUsername string
@secure()
param adminPassword string
param virtualMachineSku string

param octopusServerThumbprint string
param octopusServerUrl string
@secure()
param octopusServerApiKey string
param octopusServerRole string
param octopusServerEnvironment string
param os string
param tentacleUri string
param tentacleNamePostfix string

var networkSecurityGroupName = '${virtualMachineName}-nsg'
var networkInterfaceName = '${virtualMachineName}-nic'
var publicIpAddressName = '${virtualMachineName}-ip'
var nsgId = resourceId(resourceGroup().name, 'Microsoft.Network/networkSecurityGroups', networkSecurityGroupName)
var vnetId = resourceId(resourceGroup().name, 'Microsoft.Network/virtualNetworks', virtualNetworkName)
var subnetRef = '${vnetId}/subnets/default'
var virtualNetworkName = '${virtualMachineName}-vnet'

resource networkInterface 'Microsoft.Network/networkInterfaces@2022-11-01' = {
  name: networkInterfaceName
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          subnet: {
            id: subnetRef
          }
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: resourceId(resourceGroup().name, 'Microsoft.Network/publicIpAddresses', publicIpAddressName)
            properties: {
              deleteOption: 'Delete'
            }
          }
        }
      }
    ]
    networkSecurityGroup: {
      id: nsgId
    }
  }
  dependsOn: [
    networkSecurityGroup
    virtualNetwork
    publicIpAddress
  ]
}

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2019-02-01' = {
  name: networkSecurityGroupName
  location: location
  properties: {
    securityRules: [
      {
        name: 'RDP'
        properties: {
          priority: 300
          protocol: 'TCP'
          access: 'Allow'
          direction: 'Inbound'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '3389'
        }
      }
      {
        name: 'TentaclListen'
        properties: {
          priority: 400
          protocol: 'TCP'
          access: 'Allow'
          direction: 'Inbound'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '10933'
        }
      }
    ]
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: virtualNetworkName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '10.0.0.0/24'
        }
      }
    ]
  }
}

resource publicIpAddress 'Microsoft.Network/publicIpAddresses@2020-08-01' = {
  name: publicIpAddressName
  location: location
  properties: {
    publicIPAllocationMethod: 'Static'
  }
  sku: {
    name: 'Standard'
  }
}

resource virtualMachine 'Microsoft.Compute/virtualMachines@2022-03-01' = {
  name: virtualMachineName
  location: location
  properties: {
    hardwareProfile: {
      vmSize: 'Standard_DS1_v2'
    }
    storageProfile: {
      osDisk: {
        createOption: 'fromImage'
        managedDisk: {
          storageAccountType: 'StandardSSD_LRS'
        }
        deleteOption: 'Delete'
      }
      imageReference: {
        publisher: 'MicrosoftWindowsServer'
        offer: 'WindowsServer'
        sku: virtualMachineSku
        version: 'latest'
      }
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: networkInterface.id
          properties: {
            deleteOption: 'Delete'
          }
        }
      ]
    }
    osProfile: {
      computerName: substring(virtualMachineName, 0, 15)
      adminUsername: adminUsername
      adminPassword: adminPassword
      windowsConfiguration: {
        enableAutomaticUpdates: true
        provisionVMAgent: true
        patchSettings: {
          enableHotpatching: false
          patchMode: 'AutomaticByOS'
        }
      }
    }
    diagnosticsProfile: {
      bootDiagnostics: {
        enabled: true
      }
    }
  }
}

resource deploymentscript 'Microsoft.Compute/virtualMachines/runCommands@2022-03-01' = {
  parent: virtualMachine
  name: 'InstallTentalce'
  location: location
  properties: {
    source: {
      script: loadTextContent('./install-tentacle.ps1')
    }
    parameters: [
      {
        name: 'octopusServerThumbprint'
        value: octopusServerThumbprint
      }
      {
        name: 'octopusServerUrl'
        value: octopusServerUrl
      }      
      {
        name: 'octopusServerRole'
        value: octopusServerRole
      }
      {
        name: 'octopusServerEnvironment'
        value: octopusServerEnvironment
      }
      {
        name: 'os'
        value: os
      }
      {
        name: 'tentacleUri'
        value: tentacleUri
      }
      {
        name: 'tentacleNamePostfix'
        value: tentacleNamePostfix
      }
    ]
    protectedParameters: [
      {
        name: 'octopusServerApiKey'
        value: octopusServerApiKey
      }
    ]    
  }
}

output adminUsername string = adminUsername
// Use these if you wish to output the script results somewhere
//output errorBlobUri string = deploymentscript.properties.errorBlobUri
//output outputBlobUri string = deploymentscript.properties.outputBlobUri
