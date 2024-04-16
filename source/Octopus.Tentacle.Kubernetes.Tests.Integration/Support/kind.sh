#!/bin/bash
set -o errexit

kind_cluster_name="$1"
kind_config_path="$2"

# if the cluster is not already created
if kind get clusters | ! grep -q "$kind_cluster_name"; then
  kind create cluster --name="$kind_cluster_name" --config="$kind_config_path" --kubeconfig="$kind_cluster_name.config"
fi