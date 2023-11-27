param ([string] $version,
[string] $serverUrl,
[string] $serverusername,
[string] $serverApiKey,
[string] $environment,
[string] $roll,
[string] $space,
[string] $port,
[string] $serverthumbprint,
[bool] $deploymentTarget,
[string] $workerPool)

$hasSpaces = !$version.StartsWith("3.")
$path = "C:\Program Files\Octopus Deploy\Tentacle$version"
$hostname = $env:computername

if($deploymentTarget){
    $typeName = "DeploymentTarget"
 }else {
    $typeName = "Worker"
 }

."$path\Tentacle.exe" create-instance --instance "WindowsListening$typeName.$version" --config "C:\OctopusListening$typeName$version\Tentacle.config"
."$path\Tentacle.exe" new-certificate --instance "WindowsListening$typeName.$version" --if-blank
."$path\Tentacle.exe" configure --instance "WindowsListening$typeName.$version" --reset-trust
."$path\Tentacle.exe" configure --instance "WindowsListening$typeName.$version" --app "C:\OctopusListening$typeName$version\Applications" --port "$port" --noListen "False"
."$path\Tentacle.exe" configure --instance  "WindowsListening$typeName.$version" --trust "$serverthumbprint"

if($deploymentTarget)
{
    if($hasSpaces)
    {
        ."$path\Tentacle.exe" register-with --instance "WindowsListening$typeName.$version" --server "$serverUrl" --name "WindowsListening$typeName.$version" --comms-style "TentaclePassive" --tentacle-comms-port "$port" --force --apiKey "$serverApiKey" --environment "$environment" --role "$roll" --space $space --publicHostName $hostname
    }
    else
    {
        ."$path\Tentacle.exe" register-with --instance "WindowsListening$typeName.$version" --server "$serverUrl" --name "WindowsListening$typeName.$version" --comms-style "TentaclePassive" --tentacle-comms-port "$port" --force --apiKey "$serverApiKey" --environment "$environment" --role "$roll" --publicHostName $hostname        
    }
}
else
{
    if($hasSpaces)
    {
        ."$path\Tentacle.exe" register-worker --instance "WindowsListening$typeName.$version" --server "$serverUrl" --name "WindowsListening$typeName.$version" --comms-style "TentaclePassive" --tentacle-comms-port "$port" --force --apiKey "$serverApiKey" --space $space --workerpool "$workerPool" --publicHostName $hostname
    }
    else
    {
        ."$path\Tentacle.exe" register-worker --instance "WindowsListening$typeName.$version" --server "$serverUrl" --name "WindowsListening$typeName.$version" --comms-style "TentaclePassive" --tentacle-comms-port "$port" --force --apiKey "$serverApiKey" --workerpool "$workerPool" --publicHostName $hostname        
    }
}

."$path\Tentacle.exe" service --instance "WindowsListening$typeName.$version" --install --stop --start