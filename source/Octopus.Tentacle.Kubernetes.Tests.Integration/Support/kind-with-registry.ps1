param(
    [Parameter(Required = $true)]
    [string]
    $ClusterName
)

& kind create cluster --name $ClusterName