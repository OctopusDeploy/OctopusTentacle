$Platform = "amd64"

$CreateRegistryIfNotFound = $true
$RegistryImage = "registry:2" # This is the registry from the CNCF's Distribution project: https://github.com/distribution/distribution
$RegistryPort = "5001"  # Setup local registry on port 5001 so it doesn't conflict with Octopus' frontend
$RegistryName = "registry" # Want a different name for your local registry? Change this.

if ($CreateRegistryIfNotFound) {
    # Check if a container with the "$RegistryImage" image is running
    $containerExists = docker ps -q --filter "ancestor=$RegistryImage" | ForEach-Object { $_.Trim() }

    if ($null -eq $containerExists) {
        # Run local registry
        docker run -d -p $RegistryPort:5000 --restart always --name $RegistryName $RegistryImage
        Write-Host "Registry container with image '$RegistryImage' is now running."
    }
    else {
        # Registry is already running
        Write-Host "Registry container with image '$RegistryImage' is already running."
    }
}

$createdImageName = "alpine-agent"

docker build --tag $createdImageName --tag localhost:$RegistryPort/$createdImageName --build-arg PLATFORM=$Platform .
docker push localhost:$RegistryPort/$createdImageName
