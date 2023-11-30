param (
    [Parameter(Mandatory=$True)]
    [string]
    $BuildNumber,
    
    [Parameter()]
    [string]
    $BuildArch = "amd64",

    
    [Parameter()]
    [string]
    $LocalRegistryDomain = "localhost:5500"
)

$env:BUILD_NUMBER=$BuildNumber
$env:BUILD_DATE= Get-Date -Format "yyyy-MM-dd"
$env:BUILD_ARCH=$BuildArch

& docker compose -f docker-compose.build.yml -v build --pull octopusdeploy-kubernetes-tentacle-linux

& docker tag "docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle:$BuildNumber-linux-$BuildArch" "$LocalRegistryDomain/kubernetes-tentacle"

& docker push "$LocalRegistryDomain/kubernetes-tentacle"