param (    
    [string]
    $BuildArch = "amd64",
    
    [string]
    $LocalRegistryDomain = "localhost:5500",

    [switch]
    $NonMinikubeRegistry = $false,

    [switch]
    $BuildDebugImage
)

$nukeTargetName = if (!$BuildDebugImage) { "BuildAndLoadLocallyKubernetesTentacleContainerImage" } else { "BuildAndLoadLocalDebugKubernetesTentacleContainerImage" }

& .\build.ps1 -Target $nukeTargetName -DockerBuilder "container" -DockerPlatform "linux/$BuildArch"

if(!$?){
    Write-Error "Failed to build"
    exit 1
}


#find the most recent tag
$tags = & docker images octopusdeploy/kubernetes-agent-tentacle --format "{{.Tag}}"
$splitTags = $tags.Split([System.Environment]::NewLine)
$tag = $splitTags[0]

$artifactoryImage = "octopusdeploy/kubernetes-agent-tentacle"
$localImage = "$LocalRegistryDomain/kubernetes-agent-tentacle"

$builtImage = "$($artifactoryImage):$tag"
$builtLocalImage = "$($localImage):$tag"

& docker tag $builtImage $builtLocalImage

if (!$NonMinikubeRegistry) {
    $registryPort = ($LocalRegistryDomain -split ":")[-1]

    Write-Output "Setting kubectl context to 'minikube'"
    & kubectl config use-context minikube

    Write-Output "Forwarding minikube registry on port $registryPort"
    $portForwardProcess = Start-Process kubectl -ArgumentList "port-forward --namespace kube-system service/registry $($registryPort):80" -NoNewWindow -PassThru

    # if this a chocolately shim'd kubectl, find the original process
    if ($portForwardProcess.Description -imatch "shimgen") {
        $portForwardProcess = Get-Process kubectl | Where-Object { $_.Parent.Id -eq $portForwardProcess.Id }
    }
    
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