function Get-IPAddress()  {
	param ([ValidateNotNullOrEmpty()][string]$network, [ValidateNotNullOrEmpty()][string]$container)

    $docker = (docker inspect $container | convertfrom-json)[0]
    return $docker.NetworkSettings.Networks.$network.IpAddress
}

function Wait-ForServiceToPassHealthCheck() {
    param ([ValidateNotNullOrEmpty()][string]$serviceName)

    $attempts = 0;
    $sleepsecs = 10;
    while ($attempts -lt 50)
    {
        $attempts++
        $state = ($(docker inspect $serviceName) | ConvertFrom-Json).State
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        $health = $state.Health.Status;
        Write-Host "Waiting for $serviceName to be healthy (current: $health)..."
        if ($health -eq "healthy"){
            break;
        }

        if ($state.Status -eq "exited"){
            Write-Error "$serviceName appears to have already failed and exited."
            docker logs $serviceName > .\Temp\ConsoleLogs\$($serviceName).log
            exit 1
        }

        Sleep -Seconds $sleepsecs
    }

    if ((($(docker inspect $serviceName) | ConvertFrom-Json).State.Health.Status) -ne "healthy"){
        Write-DebugInfo @($serviceName)
        Write-Error "Octopus container $serviceName failed to go healthy after $($attempts * $sleepsecs) seconds";
        docker logs $serviceName > .\Temp\ConsoleLogs\$($serviceName).log
        exit 1;
    }
}
