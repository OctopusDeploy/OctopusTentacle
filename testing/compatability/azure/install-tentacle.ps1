param($octopusServerThumbprint,
$octopusServerUrl,
$octopusServerApiKey,
$octopusServerRole,
$octopusServerEnvironment,
$os,
$tentacleVersion)

[Enum]::GetNames([Net.SecurityProtocolType]) -contains 'Tls12'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$tentacleUri = " https://octopus-downloads.s3.amazonaws.com/octopus/Octopus.Tentacle.$tentacleVersion-net6.0-win-x64.msi"

$tentacleMsiFilename = "tentacle.msi"

Write-Host "Downloading Tentacle installer from $tentacleUri"
(New-Object System.Net.WebClient).DownloadFile($tentacleUri, $tentacleMsiFilename)

Write-Host "Installing Tentacle"
$result = start-process "msiexec" -ArgumentList @("/a", $tentacleMsiFilename, "/qn") -wait -passthru
if ($result.ExitCode -ne 0) 
{ 
    exit $result.ExitCode 
}

$tentacleExe = "C:\PFiles\Octopus Deploy\Tentacle\Tentacle.exe"
& $tentacleExe version --format=json

Write-Host "------------------------------"
Write-Host "------------------------------"
Write-Host "Configuring Listening Tentacle"
Write-Host "------------------------------"
Write-Host "------------------------------"

$ip = Invoke-RestMethod http://ipinfo.io/json

& $tentacleExe create-instance --instance "TentacleListening" --config "C:\Octopus\TentacleListening.config" --console
& $tentacleExe new-certificate --instance "TentacleListening" --if-blank --console
& $tentacleExe configure --instance "TentacleListening" --reset-trust --console
& $tentacleExe configure --instance "TentacleListening" --home "C:\Octopus\TentacleListening" --app "C:\Octopus\TentacleListening\Applications" --port "10933" --console
& $tentacleExe configure --instance "TentacleListening" --trust $octopusServerThumbprint --console
&"netsh" advfirewall firewall add rule "name=Octopus Deploy Tentacle" dir=in action=allow protocol=TCP localport=10933
& $tentacleExe register-with --instance "TentacleListening" --server $octopusServerUrl --name "listening-$os" --apiKey=$octopusServerApiKey --role $octopusServerRole --environment $octopusServerEnvironment --comms-style TentaclePassive --publicHostName $ip.Ip --console
& $tentacleExe service --instance "TentacleListening" --install --start --console

Write-Host "----------------------------"
Write-Host "----------------------------"
Write-Host "Configuring Polling Tentacle"
Write-Host "----------------------------"
Write-Host "----------------------------"
 
& $tentacleExe create-instance --instance "TentaclePolling" --config "C:\Octopus\TentaclePolling.config" --console
& $tentacleExe new-certificate --instance "TentaclePolling" --if-blank --console
& $tentacleExe configure --instance "TentaclePolling" --reset-trust --console
& $tentacleExe configure --instance "TentaclePolling" --home "C:\Octopus\TentaclePolling" --app "C:\Octopus\TentaclePolling\Applications" --noListen "True" --console
& $tentacleExe register-with --instance "TentaclePolling" --server $octopusServerUrl --name "polling-$os" --apiKey=$octopusServerApiKey --role $octopusServerRole --environment $octopusServerEnvironment --comms-style TentacleActive --server-comms-port "10943" --force --console
& $tentacleExe service --instance "TentaclePolling" --install --start --console