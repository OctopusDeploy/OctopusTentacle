param([string] $serverUrl,
[string] $serverPollingPort,
[string] $serverApiKey,
[string] $environment,
[string] $pollingRole,
[string] $listeningRole,
[string] $space,
[string] $tentacleVersions,
[string] $listening,
[string] $polling,
[string] $deploymentTargets,
[string] $workers,
[string] $serverThumbprint,
[string] $workerPool)

echo "serverUrl:$serverUrl"
echo "serverPollingPort:$serverPollingPort"
echo "serverApiKey:$serverApiKey"
echo "environment:$environment"
echo "listeningRole:$listeningRole"
echo "pollingRole:$pollingRole"
echo "space:$space"
echo "workerPool:$workerPool"

Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False

$versions = $tentacleVersions.Split(",")

echo "verisons:$versions"
echo "listening:$listening"
echo "polling:$polling"

echo "deploymentTargets:$deploymentTargets"
echo "workers:$workers"

cd c:\tmp\

$port = 10940

foreach ( $version in $versions )
{
    .\install.ps1 -version $version

    if($polling.ToLower() -eq "true")
    {
        if($deploymentTargets.ToLower() -eq "true")
        {
            .\polling.ps1 -deploymentTarget $True -version "$version" -serverUrl "$serverUrl" -serverPollingPort "$serverPollingPort" -serverApiKey "$serverApiKey" -environment "$environment" -roll "$listeningRole" -space "$space" -workerpool "na"
        }

        if($workers.ToLower() -eq "true")
        {
            .\polling.ps1 -deploymentTarget $False -version "$version" -serverUrl "$serverUrl" -serverPollingPort "$serverPollingPort" -serverApiKey "$serverApiKey" -environment "na" -roll "na" -space "$space" -workerpool "$workerPool"
        }
    }

    if($listening.ToLower() -eq "true")
    {
        if($deploymentTargets.ToLower() -eq "true")
        {
            .\listening.ps1 -deploymentTarget $True -version "$version" -serverUrl "$serverUrl" -serverApiKey "$serverApiKey" -environment "$environment" -roll "$pollingRole" -space "$space" -port $port.ToString() -serverthumbprint $serverThumbprint -workerpool "na"
            $port = $port + 1
        }

        if($workers.ToLower() -eq "true")
        {
            .\listening.ps1 -deploymentTarget $False -version "$version" -serverUrl "$serverUrl" -serverApiKey "$serverApiKey" -environment "na" -roll "na" -space "$space" -port $port.ToString() -serverthumbprint $serverThumbprint -workerpool "$workerPool"
            $port = $port + 1
        }
    }
}

# Restart-Service -Name "OctopusDeploy Tentacle:*" -Force -Verbose