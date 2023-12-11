param($octopusServerThumbprint,
$octopusServerUrl,
$octopusServerApiKey,
$octopusServerRole,
$octopusServerEnvironment,
$os,
$tentacleUri,
$tentacleNamePostfix)

# Record the arguments this script was called with, so that it is easy to run later.
myargs = "$octopusServerThumbprint $octopusServerUrl $octopusServerApiKey $octopusServerRole $octopusServerEnvironment $os $tentacleUri $tentacleNamePostfix"
$altCmd = "$PSCommandPath $myargs"
$altCmd > c:\\TentacleInstallRun.ps1

[Enum]::GetNames([Net.SecurityProtocolType]) -contains 'Tls12'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

Write-Host "Installing .NET 4.8"

$installedVersion = [int](Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').'Release'
$net48BuildNumber = 528040

if ($installedVersion -lt $net48BuildNumber) {
    Write-Host "Downloading net framework 4.8"
    invoke-webrequest "https://download.visualstudio.microsoft.com/download/pr/014120d7-d689-4305-befd-3cb711108212/0fd66638cde16859462a6243a4629a50/ndp48-x86-x64-allos-enu.exe" -OutFile "C:\Windows\Temp\ndp48-x86-x64-allos-enu.exe"
    Write-Host "Installing net framework 4.8"
    $process = start-process "C:\Windows\Temp\ndp48-x86-x64-allos-enu.exe" -argumentlist @("/q", "/norestart", "/log", "C:\Windows\Temp\ndp48-x86-x64-allos-enu.log") -wait -PassThru
    $process.WaitForExit()
	Write-Host "Installed .NET 4.8"
    # For net48 to work the machine must be rebooted, and this script re-run.
} else {
    Write-Host "Net framework 4.8 already installed"
}


$tentacleMsiFilename = "tentacle.msi"

Write-Host "Downloading Tentacle installer from $tentacleUri"
(New-Object System.Net.WebClient).DownloadFile($tentacleUri, $tentacleMsiFilename)

# Set environment variables here e.g.:
# [System.Environment]::SetEnvironmentVariable('TentacleTcpKeepAliveEnabled','true', 'Machine')

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
& $tentacleExe register-with --instance "TentacleListening" --server $octopusServerUrl --name "listening-$os-$tentacleNamePostfix" --apiKey=$octopusServerApiKey --role $octopusServerRole --environment $octopusServerEnvironment --comms-style TentaclePassive --publicHostName $ip.Ip --console
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
& $tentacleExe register-with --instance "TentaclePolling" --server $octopusServerUrl --name "polling-$os-$tentacleNamePostfix" --apiKey=$octopusServerApiKey --role $octopusServerRole --environment $octopusServerEnvironment --comms-style TentacleActive --server-comms-port "10943" --force --console
& $tentacleExe service --instance "TentaclePolling" --install --start --console

