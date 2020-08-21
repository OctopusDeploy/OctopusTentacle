param (
	[ValidateNotNullOrEmpty()]
	[string]$ProjectName,
	[ValidateNotNullOrEmpty()]
	[string]$OctopusVersion,
	[ValidateNotNullOrEmpty()]
	[string]$TentacleVersion
)

. .\common.ps1

$networkName = "${ProjectName}_default"
$octopusServerContainerName = "${ProjectName}_octopus-server_1"
Write-Host "Octopus Server container is $octopusServerContainerName"
$octopusServerIpAddress = Get-IPAddress $networkName $octopusServerContainerName
Write-Host "Octopus Server hosted at $octopusServerIpAddress"

# Ensure that the artifacts directory exists so that we can drop test results into it
New-Item -ItemType Directory -Force -Path ../artifacts | Out-Null

$TestResult = Invoke-Pester `
	-PassThru `
	-Script @{
		Path = './Tests/*.Tests.ps1';
		Parameters = @{
			IPAddress = $octopusServerIpAddress;
			OctopusUsername="admin";
			OctopusPassword="Passw0rd123";
			OctopusVersion=$OctopusVersion;
			TentacleVersion=$TentacleVersion;
			ProjectName=$ProjectName
		}
	} `
	-OutputFile ../artifacts/TestResults.xml `
	-OutputFormat NUnitXml

if($TestResult.FailedCount -ne 0) {
	Write-Host "Failed $($TestResult.FailedCount)/$($TestResult.TotalCount) Tests"
	Exit 1
} else {
	Write-Host "All $($TestResult.TotalCount) Tests Passed";
}
