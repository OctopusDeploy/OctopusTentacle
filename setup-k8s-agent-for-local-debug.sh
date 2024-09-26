#!/bin/bash

echo "This script will create a kind cluster configured for debugging the k8s agent locally in Rider"
echo "NOTE: This has only been tested on MacOS"
echo "Requirements: Docker Desktop, Kind CLI, Go CLI, Rider"
echo "continue? Y/n"

ans=Y
read -r ans

if [ -n "$ans" ] && [ "$ans" != "Y" ] && [ "$ans" != "y" ]; then
  exit 0
fi

echo ""
echo ""
echo "🏗️  - Installing kind cluster with name \"kind-k8s-agent-debug-cluster\""
kind create cluster --config k8s-agent-debug-cluster-config/k8s-agent-debug-cluster.yaml

if [ $? -ne 0 ]; then
  echo ""
  echo ""
  echo "❗  - Failed to create kind cluster, please check errors above. Aborting."
  exit -1
fi

echo "🏗️  - Adding local storage class to kind cluster called \"local-storage\""
kubectl apply -f k8s-agent-debug-cluster-config/local-storage-class.yaml

if [ $? -ne 0 ]; then
  echo ""
  echo ""
  echo "❗  - Failed to create local storage class. Aborting."
  exit -1
fi

echo "🏗️  - Adding local persistent volume to kind cluster"
kubectl apply -f k8s-agent-debug-cluster-config/local-persistent-volume.yaml

if [ $? -ne 0 ]; then
  echo "❗  - Failed to create local persisten volume. Aborting."
  exit -1
fi

echo "🏗️  - Building bootstrap runner"
env GOOS=linux go build -C docker/kubernetes-agent-tentacle/bootstrapRunner -ldflags "-s -w" -o "/tmp/k8s-agent-debug-vol/bootstrapRunner"

if [ $? -ne 0 ]; then
  echo ""
  echo ""
  echo "❗  - Failed to build bootstrap runner. Aborting."
  exit -1
fi

echo ""
echo ""
echo "The cluster is ready! 👌"
printf "Now generate the k8s agent helm command from your Octopus instance:\n- ADD \"local-storage\" as the storage class for your agent.\n- CHOOSE \"Docker Desktop\" in the DEV dropdown list.\n- RUN the install script against the new kind cluster (called \"kind-k8s-agent-debug-cluster\").\n\nWhen the installation is complete and the agent has successfully completed a health check, continue executing this script!"

while [ -z "$agentName" ]
do
  echo ""
  echo ""
  echo "❔  - What name did you give your agent?"

  read -r agentName
done

echo ""
echo ""
echo "🏗️  - Scaling down agent \"$agentName\" deployment in cluster"
kubectl scale --replicas=0 deployment/octopus-agent-tentacle -n "octopus-agent-$agentName"
echo "🏗️  - Changing the tentacle config to use localhost"
configMapContents=`kubectl get configmap tentacle-config -n "octopus-agent-$agentName" -o yaml`
configMapContents="${configMapContents//host\.docker\.internal/localhost}"
echo "🏗️  - Changing home dir and applications dir to use /tmp/k8s-agent-debug-vol"
configMapContents="${configMapContents//\/octopus//tmp/k8s-agent-debug-vol}"
echo "$configMapContents" | kubectl apply -f -

if [ -z "$KUBECONFIG" ]; then
  kubeConfig=~/.kube/config
else
  kubeConfig="$KUBECONFIG"
fi

echo ""
echo ""
echo "🏗️  - Copying launch settings for Octopus.Tentacle project"
launchSettings=`cat k8s-agent-debug-cluster-config/launchSettings.template.json`
launchSettings=${launchSettings//\<agent-name\>/"$agentName"}
launchSettings=${launchSettings//\<kube-config\>/"$kubeConfig"}
echo "$launchSettings" > "./source/Octopus.Tentacle/Properties/launchSettings.json"

echo ""
echo ""
echo "Done! 🚀"
echo ""
echo "You can now run/debug the Agent using the launch settings in Octopus.Tentacle/Properties/launchSettings.json."
echo "Check the run configuration in Rider to ensure that you're running it with the correct .net Framework version (net8.0)"
