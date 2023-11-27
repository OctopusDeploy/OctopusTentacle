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
$path = "/opt/octopus/tentacle$version"

if($deploymentTarget){
    $typeName = "DeploymentTarget"
 }else {
    $typeName = "Worker"
 }

."$path/Tentacle" create-instance --instance "LinuxPolling$typeName.$version" --config "/opt/OctopusPolling$typeName$version/Tentacle.config"
."$path/Tentacle" new-certificate --instance "LinuxPolling$typeName.$version" --if-blank
."$path/Tentacle" configure --instance "LinuxPolling$typeName.$version" --reset-trust
."$path/Tentacle" configure --instance "LinuxPolling$typeName.$version" --app "/opt/OctopusPolling$typeName$version/Applications" --port "10933" --noListen "True"
."$path/Tentacle" polling-proxy --instance "LinuxPolling$typeName.$version" --proxyEnable "False"

if($deploymentTarget)
{
    if($hasSpaces)
    {
        ."$path/Tentacle" register-with --instance "LinuxPolling$typeName.$version" --server "$serverUrl" --name "LinuxPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --environment "$environment" --role "$roll" --space $space
    }
    else
    {
        ."$path/Tentacle" register-with --instance "LinuxPolling$typeName.$version" --server "$serverUrl" --name "LinuxPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --environment "$environment" --role "$roll"
    }
}
else 
{
    if($hasSpaces)
    {
        ."$path/Tentacle" register-worker --instance "LinuxPolling$typeName.$version" --server "$serverUrl" --name "LinuxPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --space $space --workerpool "$workerPool"
    }
    else
    {
        ."$path/Tentacle" register-worker --instance "LinuxPolling$typeName.$version" --server "$serverUrl" --name "LinuxPolling$typeName.$version" --comms-style "TentacleActive" --server-comms-port "$serverPollingPort" --force --apiKey "$serverApiKey" --workerpool "$workerPool"
    }
}

."$path/Tentacle" service --instance "LinuxPolling$typeName.$version" --install --stop --start