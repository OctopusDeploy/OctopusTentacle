# Pester 5 doesn't yet support parameterised tests
[string]$IPAddress = $env:IPAddress
[string]$OctopusUsername = $env:OctopusUsername
[string]$OctopusPassword = $env:OctopusPassword
[string]$TentacleVersion = $env:TentacleVersion
[string]$ProjectName = $env:ProjectName

$OctopusURI = "http://$($IPAddress):8080"

function Registration-Tests($tentacles) {
	it 'should have been registered' {
		$tentacles.Count | Should -Be 1
	}

	it 'should be healthy' {
		$isHealthy = $tentacles[0].HealthStatus -eq "Healthy" -or $tentacles[0].HealthStatus -eq "HasWarnings"
		$isHealthy | Should -Be $true
	}

	it 'should have the correct version installed' {
		$tentacles[0].Endpoint.TentacleVersionDetails.Version | Should -Be $TentacleVersion
	}
}

Describe 'Octopus Registration' {
	if ([System.Environment]::OSVersion.Version -eq '10.0.14393.0') {
		Write-Warning "This test does not run successfully on Windows 2016"
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
	$machineIds = $machines | ForEach-Object { $_.Id }

	Write-Host "Updating Calamari..."
	$task = $repository.Tasks.ExecuteCalamariUpdate($null, $machineIds);
	$repository.Tasks.WaitForCompletion($task, 4, 3);

	Write-Host "Executing health check..."
	$task = $repository.Tasks.ExecuteHealthCheck();
	$repository.Tasks.WaitForCompletion($task, 4, 3);

	$Machines = $repository.Machines.FindAll()

	Context 'Polling Tentacle' {
		$tentacles = $($Machines | Where-Object { $_.Endpoint.CommunicationStyle -eq [Octopus.Client.Model.CommunicationStyle]::TentacleActive })
		Registration-Tests $tentacles
	}

	Context 'Listening Tentacle' {
		$tentacles = $($Machines | Where-Object { $_.Endpoint.CommunicationStyle -eq [Octopus.Client.Model.CommunicationStyle]::TentaclePassive })
		Registration-Tests $tentacles
	}
}
