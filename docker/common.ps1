function Get-IPAddress()  {
	param ([string]$network, [string]$container)

    $docker = (docker inspect $container | convertfrom-json)[0]
    return $docker.NetworkSettings.Networks.$network.IpAddress
}
