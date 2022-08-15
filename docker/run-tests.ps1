param (
	[ValidateNotNullOrEmpty()]
	[string]$ProjectName,
	[ValidateNotNullOrEmpty()]
	[string]$OctopusVersion,
	[ValidateNotNullOrEmpty()]
	[string]$TentacleVersion
)

# -SkipPublisherCheck is required because earlier versions of Pester cause conflicts if they're already installed.
Install-Module -Name "Pester" -MinimumVersion "5.0.2" -MaximumVersion "5.1.1" -Force -SkipPublisherCheck
Import-Module -Name "Pester" -MinimumVersion "5.0.2" -MaximumVersion "5.1.1" 
Write-Host "Pester Version: " (Get-Module Pester).Version

# We do this here even though it's a dependency of our tests because we can't install it more than once
# without erroring.
Install-Package Octopus.Client -source https://www.nuget.org/api/v2 -Force -SkipDependencies
Add-Type -Path (Join-Path (Get-Item ((Get-Package Octopus.Client).source)).Directory.FullName "lib/net462/Octopus.Client.dll")

. .\common.ps1

$networkName = "${ProjectName}_default"
$octopusServerContainerName = "${ProjectName}_octopus-server_1"
Write-Host "Octopus Server container is $octopusServerContainerName"
$octopusServerIpAddress = Get-IPAddress $networkName $octopusServerContainerName
Write-Host "Octopus Server hosted at $octopusServerIpAddress"
$listeningTentacleContainerName = "${ProjectName}_listening-tentacle_1"
$pollingTentacleContainerName = "${ProjectName}_polling-tentacle_1"

Wait-ForServiceToPassHealthCheck $octopusServerContainerName
Wait-ForServiceToPassHealthCheck $listeningTentacleContainerName
Wait-ForServiceToPassHealthCheck $pollingTentacleContainerName

# Ensure that the artifacts directory exists so that we can drop test results into it
New-Item -ItemType Directory -Force -Path ../artifacts | Out-Null

Write-Output "Running Pester Tests"
$configuration = [PesterConfiguration]::Default
$configuration.TestResult.Enabled = $true
$configuration.TestResult.OutputPath = '../artifacts/TestResults.xml'
$configuration.TestResult.OutputFormat = 'NUnitXml'
$configuration.Run.PassThru = $true
$configuration.Run.Path = "./Tests/*.Tests.ps1"
$configuration.Should.ErrorAction = 'Continue'

$env:IPAddress = $octopusServerIpAddress;
$env:OctopusUsername = "admin";
$env:OctopusPassword = "Passw0rd123";
$env:OctopusVersion = $OctopusVersion;
$env:TentacleVersion = $TentacleVersion;
$env:ProjectName = $ProjectName

Invoke-Pester -Configuration $configuration
