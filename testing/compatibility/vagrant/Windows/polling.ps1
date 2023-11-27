param ([string] $version,
[string] $serverUrl,
[string] $serverPollingPort,
[string] $serverApiKey,
[string] $environment,
[string] $roll,
[string] $space,
[bool] $deploymentTarget,
[string] $workerPool)

$hasSpaces = !$version.StartsWith("3.")
$path = "C:\Program Files\Octopus Deploy\Tentacle$version"

if($deploymentTarget){
    $typeName = "DeploymentTarget"
 }else {
    $typeName = "Worker"
 }

."$path\Tentacle.exe" create-instance --instance "WindowsPolling$typeName.$version" --config "C:\OctopusPolling$typeName$version\Tentacle.config"
."$path\Tentacle.exe" new-certificate --instance "WindowsPolling$typeName.$version" --if-blank
."$path\Tentacle.exe" configure --instance "WindowsPolling$typeName.$version" --reset-trust
."$path\Tentacle.exe" configure --instance "WindowsPolling$typeName.$version" --app "C:\OctopusPolling$typeName$version\Applications" --port "10933" --noListen "True"
."$path\Tentacle.exe" polling-proxy --instance "WindowsPolling$typeName.$version" --proxyEnable "False"

if($deploymentTarget)
{
    if($hasSpaces)
    {
        ."$path\Tentacle.exe" register-with --instance "WindowsPolling$typeName.$version" --server "$serverUrl" --name "WindowsPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --environment "$environment" --role "$roll" --space $space
    }
    else
    {
        ."$path\Tentacle.exe" register-with --instance "WindowsPolling$typeName.$version" --server "$serverUrl" --name "WindowsPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --environment "$environment" --role "$roll"
    }
}
else 
{
    if($hasSpaces)
    {
        ."$path\Tentacle.exe" register-worker --instance "WindowsPolling$typeName.$version" --server "$serverUrl" --name "WindowsPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --space $space --workerpool "$workerPool"
    }
    else
    {
        ."$path\Tentacle.exe" register-worker --instance "WindowsPolling$typeName.$version" --server "$serverUrl" --name "WindowsPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --workerpool "$workerPool"
    }
}

."$path\Tentacle.exe" service --instance "WindowsPolling$typeName.$version" --install --stop --start