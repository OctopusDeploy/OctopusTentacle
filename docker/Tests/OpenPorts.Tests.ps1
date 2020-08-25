# Pester 5 doesn't yet support parameterised tests
[string]$IPAddress = $env:IPAddress
[string]$OctopusUsername = $env:OctopusUsername
[string]$OctopusPassword = $env:OctopusPassword
[string]$TentacleVersion = $env:TentacleVersion
[string]$ProjectName = $env:ProjectName

. .\common.ps1

$networkName = $ProjectName + "_default";
$octopusServerContainer = $ProjectName + "_octopus-server_1";
$octopusListeningTentacleContainer = $ProjectName + "_listening-tentacle_1";
$octopusPollingTentacleContainer = $ProjectName + "_polling-tentacle_1";
$octopusDBContainer = $ProjectName + "_db_1";

Describe 'Port 10933' {

	Context 'Listening Tentacle' {
		$listeningTentacleIPAddress = $(Get-IPAddress $networkName $octopusListeningTentacleContainer)

		Write-Host "Testing connectivity to listening tentacle at $listeningTentacleIPAddress"
		$result = Test-NetConnection -Port 10933 -ComputerName $listeningTentacleIPAddress -InformationLevel "Quiet"
		
		it 'should be open' {
			$result | Should -Be $true
		}
	}

	# Context 'Polling Tentacle' {
	# 	$PollingTentacleIPAddress = $(Get-IPAddress $octopusPollingTentacleContainer)
	# 	$result = Test-NetConnection -Port 10933 -ComputerName $PollingTentacleIPAddress -InformationLevel "Quiet"
	# 	it 'should not be open' {
	# 		$result | Should -Be $false
	# 	}
	# }
}