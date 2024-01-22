param (    
    [string]
    $BuildArch = "amd64",
    
    [string]
    $LocalRegistryDomain = "localhost:5500",

    [switch]
    $NonMinikubeRegistry = $false,

    [switch]
    $BuildDebugImage,

    [switch]
    $SkipBuild
)

$runtimeToBuild = "linux-$BuildArch".Replace("amd", "x")

#First we pack the unsigned builds
if (!$SkipBuild) {
    & .\build.ps1 -Target "PackLinuxUnsigned" -RuntimeId $runtimeToBuild
}

#Now find the latest package version
$package = Get-ChildItem -Path "$PSScriptRoot/_artifacts/deb" -Filter "tentacle_*_$BuildArch.deb"
$packageNameParts = $package.Name -Split "_"
$buildNumber = $packageNameParts[1]

Write-Output "Using package $($package.Name)"

$env:BUILD_NUMBER = $buildNumber
$env:BUILD_DATE = Get-Date -Format "yyyy-MM-dd"
$env:BUILD_ARCH = $BuildArch

$baseTag = "$buildNumber-linux-$BuildArch"
$tag = $baseTag

$artifactoryImage = "docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle"
$localImage = "$LocalRegistryDomain/kubernetes-tentacle"

Write-Output "Building image"

& docker compose -f docker-compose.build.yml -v build --pull octopusdeploy-kubernetes-tentacle-linux

$builtImage = "$($artifactoryImage):$tag"
$builtLocalImage = "$($localImage):$tag"

if ($BuildDebugImage) {
    Write-Output "Building debug image"
    
    $env:IMAGE_TAG = $tag
    $env:DEBUGGER_ARCH = $runtimeToBuild

    & docker compose -f docker-compose.build-dev.yml -v build octopusdeploy-kubernetes-tentacle-linux
    
    $tag = "$tag-debug"    
    $builtImage = "$($artifactoryImage):$tag"
    $builtLocalImage = "$($localImage):$tag"
}


& docker tag $builtImage $builtLocalImage


if (!$NonMinikubeRegistry) {
    $registryPort = ($LocalRegistryDomain -split ":")[-1]

    Write-Output "Setting kubectl context to 'minikube'"
    & kubectl config use-context minikube

    Write-Output "Forwarding minikube registry on port $registryPort"
    $portForwardProcess = Start-Process kubectl -ArgumentList "port-forward --namespace kube-system service/registry $($registryPort):80" -NoNewWindow -PassThru
    
    Write-Output "Running network forwarding docker container"
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

Write-Output "Pushing $builtLocalImage"

& docker push $builtLocalImage

Write-Output "Pushed $builtLocalImage"


if (!$NonMinikubeRegistry) {
    Write-Output "Stopping network forwarding docker container"
    & docker stop $containerId | Out-Null
    Write-Output "Stopping minikube port forwarding"
    Stop-Process -Id $portForwardProcess.Id -ErrorAction SilentlyContinue
}

Write-Output "---------"

if ($BuildDebugImage) {
    Write-Output "Base Image: $($artifactoryImage):$baseTag"
}
Write-Output "Built Image: $builtImage"
Write-Output "Built Local Image: $builtLocalImage"
Write-Output "Built Tag: $tag"
if (!$NonMinikubeRegistry) {
    Write-Output "Minikube Image: $($builtLocalImage.Replace($LocalRegistryDomain, "localhost:5000"))"
}

Write-Output "---------"