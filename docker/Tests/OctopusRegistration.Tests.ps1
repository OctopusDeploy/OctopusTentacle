# Pester 5 doesn't yet support parameterised tests
[string]$script:IPAddress = $env:IPAddress
[string]$script:OctopusUsername = $env:OctopusUsername
[string]$script:OctopusPassword = $env:OctopusPassword
[string]$script:TentacleVersion = $env:TentacleVersion
[string]$script:ProjectName = $env:ProjectName

Install-Package Octopus.Client -source https://www.nuget.org/api/v2 -Force -SkipDependencies
Add-Type -Path (Join-Path (Get-Item ((Get-Package Octopus.Client).source)).Directory.FullName "lib/net452/Octopus.Client.dll")

function script:New-OctopusRepository() {
	$octopusURI = "http://$($script:IPAddress):8080"
	Write-Host "Using Octopus server at $octopusURI"

	$endpoint = new-object Octopus.Client.OctopusServerEndpoint $octopusURI
	$repository = new-object Octopus.Client.OctopusRepository $endpoint

	$loginObj = New-Object Octopus.Client.Model.LoginCommand
	$loginObj.Username = $script:OctopusUsername
	$loginObj.Password = $script:OctopusPassword
	$repository.Users.SignIn($loginObj)

	[Octopus.Client.OctopusRepository]$repository
}

Describe 'Octopus Registration' {
	if ([System.Environment]::OSVersion.Version -eq '10.0.14393.0') {
		Write-Warning "This test does not run successfully on Windows 2016"
		return
	}

	BeforeAll {
		$repository = script:New-OctopusRepository

		Write-Host "Enumerating machines..."
		$machines = $repository.Machines.FindAll()

		Write-Host "Updating Calamari..."
		$machineIds = $machines.id
		$task = ($repository).Tasks.ExecuteCalamariUpdate($null, $machineIds);
		$repository.Tasks.WaitForCompletion($task, 4, 3);

		Write-Host "Executing health checks..."
		$task = $repository.Tasks.ExecuteHealthCheck();
		$repository.Tasks.WaitForCompletion($task, 4, 3);
	}

	Context 'Polling Tentacle' {

		BeforeAll {
			$tentacles = , ($machines | Where-Object { $_.Endpoint.CommunicationStyle -eq [Octopus.Client.Model.CommunicationStyle]::TentacleActive })
		}
		
		It 'should have been registered' {
			$tentacles.Length | Should -Be 1
		}

		it 'should be healthy' {
			$isHealthy = $tentacles[0].HealthStatus -eq "Healthy" -or $tentacle[0].HealthStatus -eq "HasWarnings"
			$isHealthy | Should -Be $true
		}

		it 'should have the correct version installed' {
			$tentacles[0].Endpoint.TentacleVersionDetails.Version | Should -Be $TentacleVersion
		}
	
	}

	Context 'Listening Tentacle' {

		BeforeAll {
			$tentacles = $($machines | Where-Object { $_.Endpoint.CommunicationStyle -eq [Octopus.Client.Model.CommunicationStyle]::TentaclePassive })
		}
		
		It 'should have been registered' {
			$tentacles.Length | Should -Be 1
		}

		it 'should be healthy' {
			$isHealthy = $tentacles[0].HealthStatus -eq "Healthy" -or $tentacle[0].HealthStatus -eq "HasWarnings"
			$isHealthy | Should -Be $true
		}

		it 'should have the correct version installed' {
			$tentacles[0].Endpoint.TentacleVersionDetails.Version | Should -Be $TentacleVersion
		}
	
	}
}
