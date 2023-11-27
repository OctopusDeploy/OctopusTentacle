# Generate the configuration

$config = Get-Content 'config.json' -raw | ConvertFrom-Json
$tentacleVersionsString = $config.tentacleVersions;
$tentacleVersions = $tentacleVersionsString.Split(',')

$versions = 1..$tentacleVersions.Count
$startPort = 11900

for ($i = 0; $i -lt $tentacleVersions.Count; $i++) {
    $v = New-Object -TypeName psobject 
    $v | Add-Member -MemberType NoteProperty -Name "version" -Value $tentacleVersions[$i]
    $v | Add-Member -MemberType NoteProperty -Name "workerListeningPort" -Value $startPort
    $v | Add-Member -MemberType NoteProperty -Name "deploymentTargetListeningPort" -Value ($startPort + 1)
    $versions[$i] = $v;

    $startPort += 2
}

$config | add-member -Name "versions" -value $versions -MemberType NoteProperty

$config | ConvertTo-Json -depth 32 | set-content 'config.generated.json'

# Install Mustache

Install-Module Poshstache -f

# Generate the docker-compose file

$jsonConfigFile = "config.generated.json"
$jsonConfig = Get-Content $jsonConfigFile | Out-String
ConvertTo-PoshstacheTemplate -InputFile "docker-compose.mustache" -ParametersObject $jsonConfig | Out-File "docker-compose.yml" -Force -Encoding "UTF8"