param(
    [Parameter(Mandatory = $false)]
    [string]
    $ClusterName = "kind",

    [Parameter(Mandatory = $true)]
    [string]
    $KindConfigPath
)

# 1. Create registry container unless it already exists
$reg_name = 'kind-registry'
$reg_port = '5555' # The 5tory of the 5ecret 5tar 5ystem

$isRunning = & docker inspect -f '{{.State.Running}}' "$reg_name"
if ($isRunning -ne "true" ) {

    & docker run `
  -d --restart=always -p "127.0.0.1:$($reg_port):5000" --network bridge --name $reg_name `
    registry:2
}

# 2. Create kind cluster with containerd registry config dir enabled
# TODO: kind will eventually enable this by default and this patch will
# be unnecessary.
#
# See:
# https://github.com/kubernetes-sigs/kind/issues/2875
# https://github.com/containerd/containerd/blob/main/docs/cri/config.md#registry-configuration
# See: https://github.com/containerd/containerd/blob/main/docs/hosts.md
$allClusters = & kind get clusters

# if the cluster is not already created
if($allClusters -notcontains $ClusterName) {
    & kind create cluster --name $ClusterName --config="$KindConfigPath" --kubeconfig="$ClusterName.config"
}

# 3. Add the registry config to the nodes
#
# This is necessary because localhost resolves to loopback addresses that are
# network-namespace local.
# In other words: localhost in the container is not localhost on the host.
#
# We want a consistent name that works from both ends, so we tell containerd to
# alias localhost:${reg_port} to the registry container when pulling images
$reg_dir="/etc/containerd/certs.d/localhost:$reg_port"
$nodes = & kind get nodes -n $ClusterName;
foreach ($node in $nodes) {
    & docker exec $node mkdir -p $reg_dir
    & docker exec -i $node bash -c "echo '[host.""http://$($reg_name):5000""]' >> $reg_dir/hosts.toml"
}

# 4. Connect the registry to the cluster network if not already connected
# This allows kind to bootstrap the network but ensures they're on the same network
$isConnectedToNetwork = & docker inspect -f='{{json .NetworkSettings.Networks.kind}}' $reg_name
if($null -eq $isConnectedToNetwork) {
    & docker network connect "kind" $reg_name
}

# 5. Document the local registry
# https://github.com/kubernetes/enhancements/tree/master/keps/sig-cluster-lifecycle/generic/1755-communicating-a-local-registry
$yaml = @"
apiVersion: v1
kind: ConfigMap
metadata:
  name: local-registry-hosting
  namespace: kube-public
data:
  localRegistryHosting.v1: |
  host: "localhost:$reg_port"
  help: "https://kind.sigs.k8s.io/docs/user/local-registry/"
"@
$yaml | kubectl --context "kind-$ClusterName" apply -f -