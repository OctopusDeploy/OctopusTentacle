#!/bin/bash
set -o errexit

kind_cluster_name="$1"

kind create cluster --name="$kind_cluster_name"