param (    
    [Parameter()]
    [string]
    $BuildArch = "amd64",
    
    [Parameter()]
    [string]
    $LocalRegistryDomain = "localhost:5500",

    [Parameter()]
    [switch]
    $NonMinikubeRegistry = $false
)

$runtimeToBuild = "linux-$BuildArch".Replace("amd", "x")

#First we pack the unsigned builds
& .\build.ps1 -Target "PackLinuxUnsigned" -RuntimeId $runtimeToBuild

#Now find the latest package version
$package = Get-ChildItem -Path "$PSScriptRoot/_artifacts/deb" -Filter "tentacle_*_$BuildArch.deb"
$packageNameParts = $package.Name -Split "_"
$buildNumber = $packageNameParts[1]

Write-Output "Using package $($package.Name)"

$env:BUILD_NUMBER = $buildNumber
$env:BUILD_DATE = Get-Date -Format "yyyy-MM-dd"
$env:BUILD_ARCH = $BuildArch

& docker compose -f docker-compose.build.yml -v build --pull octopusdeploy-kubernetes-tentacle-linux

& docker tag "docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle:$buildNumber-linux-$BuildArch" "$LocalRegistryDomain/kubernetes-tentacle:$buildNumber-linux-$BuildArch"

if (!$NonMinikubeRegistry) {
    $registryPort = ($LocalRegistryDomain -split ":")[-1]

    Write-Output "Setting kubectl context to 'minikube'"
    & kubectl config use-context minikube

    Write-Output "Forwarding minikube registry on port $registryPort"
    $portForwardProcess = Start-Process kubectl -ArgumentList "port-forward --namespace kube-system service/registry $($registryPort):80" -NoNewWindow -PassThru
    
    Write-Output "Running network forwarding docker container"
    $minikubeIP = & minikube ip
    $containerId = & docker run --rm -d --network=host alpine/socat "tcp-listen:$registryPort,reuseaddr,fork" "tcp-connect:host.docker.internal:$registryPort"

    $isRunning = $false
    Write-Output "Waiting for network forwarding docker container to be running"
    while ($isRunning -ne $true) {
        $runningValue = & docker inspect -f "{{.State.Running}}" $containerId
        $isRunning = $runningValue -eq "true"

        if ($isRunning -eq $false) {
            Start-Sleep -Milliseconds 250
        }
    }
    Write-Output "Network forwarding docker container running"
}

$imageName = "$LocalRegistryDomain/kubernetes-tentacle:$buildNumber-linux-$BuildArch"

Write-Output "Pushing $imageName"

& docker push $imageName

Write-Output "Pushed $imageName"

if (!$NonMinikubeRegistry) {
    Write-Output "Stopping network forwarding docker container"
    & docker stop $containerId | Out-Null
    Write-Output "Stopping minikube port forwarding"
    Stop-Process -Id $portForwardProcess.Id -ErrorAction SilentlyContinue
}