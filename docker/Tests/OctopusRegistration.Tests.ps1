param(
	[ValidateNotNullOrEmpty()]
	[string]$IPAddress,
	[ValidateNotNullOrEmpty()]
	[string]$OctopusUsername,
	[ValidateNotNullOrEmpty()]
	[string]$OctopusPassword,
	[ValidateNotNullOrEmpty()]
	[string]$TentacleVersion
)

Add-Type -Path './Testing/Tools/Octopus.Client.dll'

$OctopusURI = "http://$($IPAddress):8080"

function Registration-Tests($Tentacles) {
	it 'should have been registered' {
		$Tentacles.Count | should be 1
	}

	it 'should be healthy' {
		$isHealthy = $Tentacles[0].HealthStatus -eq "Healthy" -or $Tentacles[0].HealthStatus -eq "HasWarnings"
		$isHealthy | Should Be $true
	}

	it 'should have the correct version installed' {
		$Tentacles[0].Endpoint.TentacleVersionDetails.Version | should be $TentacleVersion
	}
}

Describe 'Octopus Registration' {
	if ([System.Environment]::OSVersion.Version -eq '10.0.14393.0') {
		Write-Warning "This test does not run successfully on windows 2016"
		return
	}

	Write-Host "Using Octopus server at $OctopusURI"

	Write-Host "Creating Octopus client..."
	$endpoint = new-object Octopus.Client.OctopusServerEndpoint $OctopusURI

	Write-Host "Creating Octopus repository..."
	$repository = new-object Octopus.Client.OctopusRepository $endpoint

	Write-Host "Signing in..."
	$LoginObj = New-Object Octopus.Client.Model.LoginCommand
	$LoginObj.Username = $OctopusUsername
	$LoginObj.Password = $OctopusPassword
	$repository.Users.SignIn($LoginObj)

	Write-Host "Enumerating machines..."
	$machines = $repository.Machines.FindAll()
	$machineIds = $machines | % { $_.Id }

	Write-Host "Updating Calamari..."
	$task = $repository.Tasks.ExecuteCalamariUpdate($null, $machineIds);
	$repository.Tasks.WaitForCompletion($task, 4, 3);

	Write-Host "Executing health check..."
	$task = $repository.Tasks.ExecuteHealthCheck();
	$repository.Tasks.WaitForCompletion($task, 4, 3);

	$Machines = $repository.Machines.FindAll()

	Context 'Polling Tentacle' {
		$PollingTentacles = $($Machines | where { $_.Endpoint.CommunicationStyle -eq [Octopus.Client.Model.CommunicationStyle]::TentacleActive })
		Registration-Tests $PollingTentacles
	}

	Context 'Listening Tentacle' {
		$ListeningTentacles = $($Machines | where { $_.Endpoint.CommunicationStyle -eq [Octopus.Client.Model.CommunicationStyle]::TentaclePassive })
		Registration-Tests $ListeningTentacles
	}

	# it 'should have imported the migration export' {
	# 	$DevEnv = $repository.Environments.FindByName("Development")
	# 	$DevEnv | should not be $null
	# }
}
