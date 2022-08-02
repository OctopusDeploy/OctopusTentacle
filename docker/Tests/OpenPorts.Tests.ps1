# Pester 5 doesn't yet support parameterised tests
[string]$script:IPAddress = $env:IPAddress
[string]$script:OctopusUsername = $env:OctopusUsername
[string]$script:OctopusPassword = $env:OctopusPassword
[string]$script:TentacleVersion = $env:TentacleVersion
[string]$script:ProjectName = $env:ProjectName

. .\common.ps1

Describe 'Port 10933' {

	BeforeAll {
		$networkName = $script:ProjectName + "_default";
		$octopusServerContainer = $script:ProjectName + "_octopus-server_1";
		$octopusListeningTentacleContainer = $script:ProjectName + "_listening-tentacle_1";
		$octopusPollingTentacleContainer = $script:ProjectName + "_polling-tentacle_1";
		$octopusDBContainer = $script:ProjectName + "_db_1";
	}

	Context 'Listening Tentacle' {

		it 'should be listening on 10933' {
			$listeningTentacleIPAddress = $(Get-IPAddress $networkName $octopusListeningTentacleContainer)

			Write-Host "Testing connectivity to listening tentacle at $listeningTentacleIPAddress"
			$result = Test-NetConnection -Port 10933 -ComputerName $listeningTentacleIPAddress -InformationLevel "Quiet"
			$result | Should -Be $true
		}
	}

	Context 'Polling Tentacle' {

		it 'should not be listening on 10933' {
			$pollingTentacleIPAddress = $(Get-IPAddress $networkName $octopusPollingTentacleContainer)

			$result = Test-NetConnection -Port 10933 -ComputerName $pollingTentacleIPAddress -InformationLevel "Quiet"
			$result | Should -Be $false
		}
	}
}