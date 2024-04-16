param(
    [Parameter(Mandatory = $true)]
    [string]
    $ClusterName,

    [Parameter(Mandatory = $true)]
    [string]
    $KindConfigPath
)

$allClusters = & kind get clusters

# if the cluster is not already created
if($allClusters -notcontains $ClusterName) {
    & kind create cluster --name $ClusterName --config="$KindConfigPath" --kubeconfig="$ClusterName.config"
}