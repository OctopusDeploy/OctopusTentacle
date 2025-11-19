#!/bin/bash
set -eu

if [[ "$ACCEPT_EULA" != "Y" ]]; then
  echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
  exit 1
fi

# In the scenario where a customer is using a custom certificate (which is mounted via a config map), we need to rehash the certificates
# We just do this all the time because there is no downside
# We specifically want to avoid exiting the script if this fails, as it is not critical to the operation of the tentacle
set +e
echo "Rehashing SSL/TLS certificates"
openssl rehash /etc/ssl/certs
set -e

# Tentacle Docker images only support once instance per container. Running multiple instances can be achieved by running multiple containers.
instanceName=Tentacle
internalListeningPort=10933

#If TentacleHome environment variable exists, use that
configurationDirectory=/etc/octopus
if [[ -n "$TentacleHome" ]]; then
  configurationDirectory="$TentacleHome"
fi

#If TentacleApplications environment variable exists, use that
applicationsDirectory=/home/Octopus/Applications
if [[ -n "$TentacleApplications" ]]; then
  applicationsDirectory="$TentacleApplications"
fi

mkdir -p $configurationDirectory
mkdir -p $applicationsDirectory

if [ ! -f /usr/bin/tentacle ]; then
  ln -s /opt/octopus/tentacle/Tentacle /usr/bin/tentacle
fi

function getPublicHostName() {
  if [[ "$PublicHostNameConfiguration" == "PublicIp" ]]; then
    curl https://api.ipify.org/
  elif [[ "$PublicHostNameConfiguration" == "FQDN" ]]; then
    hostname --fqdn
  elif [[ "$PublicHostNameConfiguration" == "ComputerName" ]]; then
    hostname
  else
    echo $CustomPublicHostName
  fi
}

function validateVariables() {
  validateCommonVariables

  if [[ "$DeploymentTargetEnabled" != "true" && "$WorkerEnabled" != "true" ]]; then
    echo "Please specify whether to install as a worker or a deployment target with the 'WorkerEnabled' or 'DeploymentTargetEnabled' environment variables" >&2
    exit 1
  fi

  if [[ "$DeploymentTargetEnabled" == "true" && "$WorkerEnabled" == "true" ]]; then
    echo "The installation cannot be as both a worker and a deployment target, please choose one" >&2
    exit 1
  fi

  if [[ "$DeploymentTargetEnabled" == "true" ]]; then
    validateDeploymentTargetVariables
  fi

  if [[ "$WorkerEnabled" == "true" ]]; then
    validateWorkerVariables
  fi
}

function validateCommonVariables() {
  if [[ -z "$ServerApiKey" && -z "$BearerToken" ]]; then
    if [[ -z "$ServerPassword" || -z "$ServerUsername" ]]; then
      echo "Please specify either an API key, a Bearer Token or a username/password with the 'ServerApiKey' or 'ServerUsername'/'ServerPassword' environment variables" >&2
      exit 1
    fi
  fi

  if [[ -z "$ServerUrl" ]]; then
    echo "Please specify an Octopus Server with the 'ServerUrl' environment variable" >&2
    exit 1
  fi

  echo " - server endpoint '$ServerUrl'"
  echo " - api key '##########'"
  echo " - host '$PublicHostNameConfiguration'"

  if [[ -n "$ServerCommsAddress" || -n "$ServerCommsAddresses" || -n "$ServerPort" ]]; then
    echo " - communication mode 'Kubernetes' (Polling)"

    if [[ -n "$ServerCommsAddress" ]]; then
      echo " - server comms address $ServerCommsAddress"
    fi
    if [[ -n "$ServerCommsAddresses" ]]; then
      echo " - HA server comms addresses $ServerCommsAddresses"
    fi
    if [[ -n "$ServerPort" ]]; then
      echo " - server port $ServerPort"
    fi
    if [[ -n "$ServerSubscriptionId" ]]; then
      echo " - server subscription id '$ServerSubscriptionId'"
    fi
  else
    echo " - communication mode 'Kubernetes' (Listening)"
    echo " - registered port $ListeningPort"
  fi
  
  if [[ -n "$AgentName" ]]; then
    echo " - name '$AgentName'"
  fi

  if [[ -n "$Space" ]]; then
    echo " - space '$Space'"
  fi

  if [[ -n "$TentacleCertificateBase64" ]]; then
    echo " - tentacle certificate '${TentacleCertificateBase64:0:3}...${TentacleCertificateBase64: -3}'"
  fi
}

function validateDeploymentTargetVariables() {
  if [[ -z "$TargetEnvironment" ]]; then
    echo "Please specify one or more environment names/ids/slugs (comma delimited) with the 'TargetEnvironment' environment variable" >&2
    exit 1
  fi

  if [[ -z "$TargetRole" ]]; then
    echo "Please specify one or more role names (comma delimited) with the 'TargetRole' environment variable" >&2
    exit 1
  fi

  echo " - environment '$TargetEnvironment'"
  echo " - role '$TargetRole'"

  if [[ -n "$TargetTenant" ]]; then
    echo " - tenant '$TargetTenant'"
  fi
  if [[ -n "$TargetTenantTag" ]]; then
    echo " - tenant tag '$TargetTenantTag'"
  fi
  if [[ -n "$TargetTenantedDeploymentParticipation" ]]; then
    echo " - tenanted deployment participation '$TargetTenantedDeploymentParticipation'"
  fi
  
  if [[ -n "$DefaultNamespace" ]]; then
    echo " - default namespace '$DefaultNamespace'"
  fi
}

function validateWorkerVariables() {
  if [[ -z "$WorkerPools" ]]; then
    echo "Please specify one or more worker pool names/ids/slugs (comma delimited) with the 'WorkerPools' environment variable" >&2
    exit 1
  fi

  echo " - worker pools '$WorkerPools'"
}

function migrateFromPreinstallScript() {
  tentacle migrate-preinstalled-k8s-config \
    --source-config-map-name "tentacle-config-pre" \
    --source-secret-name "tentacle-secret-pre" \
    --destination-config-map-name "tentacle-config" \
    --destination-secret-name "tentacle-secret"
}

function configureTentacle() {
  tentacle create-instance --instance "$instanceName" --config "$configurationDirectory/tentacle.config" --home "$configurationDirectory"

  echo "Setting directory paths ..."
  tentacle configure --instance "$instanceName" --app "$applicationsDirectory"

  echo "Configuring communication type ..."
  if [[ -n "$ServerCommsAddress" || -n "$ServerCommsAddresses" || -n "$ServerPort" ]]; then
    tentacle configure --instance "$instanceName" --noListen "True"
  else
    tentacle configure --instance "$instanceName" --port $internalListeningPort --noListen "False"
  fi

  if [[ -n "$TentacleCertificateBase64" ]]; then
    echo "Importing custom certificate ..."
    tentacle import-certificate --instance "$instanceName" --from-base64="$TentacleCertificateBase64"
  else
    echo "Creating certificate ..."
    tentacle new-certificate --instance "$instanceName" --if-blank
  fi
}

function setupVariablesForRegistrationCheck() {
  local namespace=$(cat /var/run/secrets/kubernetes.io/serviceaccount/namespace)
  local config_map_name="tentacle-config"
  SERVICE_URL="https://$KUBERNETES_SERVICE_HOST:$KUBERNETES_SERVICE_PORT/api/v1/namespaces/$namespace/configmaps/$config_map_name"
  SERVICE_ACCOUNT_TOKEN_PATH="/var/run/secrets/kubernetes.io/serviceaccount/token"
}

function getStatusOfRegistration() {
  echo "Checking registration status..."

  IS_REGISTERED=$(curl -s --request GET \
    --url "$SERVICE_URL" \
    --header "Authorization: Bearer $(cat $SERVICE_ACCOUNT_TOKEN_PATH)" \
    --header 'Accept: application/json' \
    --cacert /var/run/secrets/kubernetes.io/serviceaccount/ca.crt |
    grep -o '"Tentacle.Services.IsRegistered": "[^"]*"' |
    cut -d'"' -f4)
}

function registerTentacle() {
  echo "Registering with server ..."

  local ARGS=()

  if [[ "$DeploymentTargetEnabled" == "true" ]]; then
    ARGS+=('register-k8s-target')
  
    if [[ -n "$TargetEnvironment" ]]; then
      IFS=',' read -ra ENVIRONMENTS <<<"$TargetEnvironment"
      for i in "${ENVIRONMENTS[@]}"; do
        ARGS+=('--environment' "$i")
      done
    fi

    if [[ -n "$TargetRole" ]]; then
      IFS=',' read -ra ROLES <<<"$TargetRole"
      for i in "${ROLES[@]}"; do
        ARGS+=('--role' "$i")
      done
    fi

    if [[ -n "$TargetTenant" ]]; then
      IFS=',' read -ra TENANTS <<<"$TargetTenant"
      for i in "${TENANTS[@]}"; do
        ARGS+=('--tenant' "$i")
      done
    fi

    if [[ -n "$TargetTenantTag" ]]; then
      IFS=',' read -ra TENANTTAGS <<<"$TargetTenantTag"
      for i in "${TENANTTAGS[@]}"; do
        ARGS+=('--tenanttag' "$i")
      done
    fi

    if [[ -n "$TargetTenantedDeploymentParticipation" ]]; then
      ARGS+=('--tenanted-deployment-participation' "$TargetTenantedDeploymentParticipation")
    fi

    if [[ -n "$DefaultNamespace" ]]; then
      ARGS+=('--default-namespace' "$DefaultNamespace")
    fi
  elif [[ "$WorkerEnabled" == "true" ]]; then
    ARGS+=('register-k8s-worker')

    if [[ -n "$WorkerPools" ]]; then
      IFS=',' read -ra WORKERPOOLS <<<"$WorkerPools"
      for i in "${WORKERPOOLS[@]}"; do
        ARGS+=('--workerpool' "$i")
      done
    fi
  fi

  ARGS+=(
    '--instance' "$instanceName"
    '--server' "$ServerUrl"
    '--space' "$Space"
    '--policy' "$MachinePolicy")

  if [[ -n "$AgentName" ]]; then
    ARGS+=('--name' "$AgentName")
  fi

  if [[ -n "$ServerCommsAddress" || -n "$ServerCommsAddresses" || -n "$ServerPort" ]]; then
    ARGS+=('--comms-style' 'TentacleActive')

    # If ServerCommsAddress (singular) is not set, use the first value in ServerCommsAddresses (plural)
    if [[ -n "$ServerCommsAddress" ]]; then
      ARGS+=('--server-comms-address' "$ServerCommsAddress")
    elif [[ -n "$ServerCommsAddresses" ]]; then
      IFS=',' read -ra SERVER_ADDRESSES <<<"$ServerCommsAddresses"
      ARGS+=('--server-comms-address' "${SERVER_ADDRESSES[0]}")
    fi

    if [[ -n "$ServerPort" ]]; then
      ARGS+=('--server-comms-port' $ServerPort)
    fi

    if [[ -n "$ServerSubscriptionId" ]]; then
      ARGS+=('--server-subscription-id' $ServerSubscriptionId)
    fi
  else
    ARGS+=(
      '--comms-style' 'TentaclePassive'
      '--publicHostName' $(getPublicHostName))

    if [[ -n "$ListeningPort" && "$ListeningPort" != "$internalListeningPort" ]]; then
      ARGS+=('--tentacle-comms-port' $ListeningPort)
    fi
  fi

  if [[ -n "$ServerApiKey" ]]; then
    echo "Registering Tentacle with API key"
    ARGS+=('--apiKey' $ServerApiKey)
  elif [[ -n "$BearerToken" ]]; then
    echo "Registering Tentacle with Bearer Token"
    ARGS+=('--bearerToken' "$BearerToken")
  else
    echo "Registering Tentacle with username/password"
    ARGS+=(
      '--username' "$ServerUsername"
      '--password' "$ServerPassword")
  fi

  tentacle "${ARGS[@]}"
}

function addAdditionalServerInstancesIfRequired() {
  if [[ -z "$ServerCommsAddresses" ]]; then
    clearTrustedServersExcept "$ServerCommsAddress"
    return
  fi

  IFS=',' read -ra SERVER_ADDRESSES <<<"$ServerCommsAddresses"
  len=${#SERVER_ADDRESSES[@]}

  if [[ -n "$ServerCommsAddress" ]]; then
    clearTrustedServersExcept "$ServerCommsAddress"
  else
    clearTrustedServersExcept "${SERVER_ADDRESSES[0]}"
  fi

  # If ServerCommsAddress (singular) is not set and ServerCommsAddresses (plural)
  # has only one element return as nothing to do.
  if [[ -z "$ServerCommsAddress" && len -eq 1 ]]; then
    return
  # If ServerCommsAddresses (plural) is empty, return as nothing to do.
  elif [[ len -eq 0 ]]; then
    return
  fi

  echo "Registering additional HA Servers..."

  # If ServerCommsAddress (singular) is not set, skip the first value in
  # ServerCommsAddresses (plural) as the first value was used for the main
  # registration.
  if [[ -z "$ServerCommsAddress" ]]; then
    # The ":1" skips the first element of the array
    for i in "${SERVER_ADDRESSES[@]:1}"; do
      registerAdditionalServer "$i"
    done
  else
    for i in "${SERVER_ADDRESSES[@]}"; do
      registerAdditionalServer "$i"
    done
  fi
}

function clearTrustedServersExcept()
{
  tentacle clear-trusted-servers --instance "$instanceName" --keep "$1"
}

function registerAdditionalServer() {
  serverCommsAddress=$1

  echo "Registering server '${serverCommsAddress}'"

  local ARGS=()
  ARGS+=('poll-server' '--reuse-server-thumbprint')

  ARGS+=('--instance' "$instanceName"
    '--server' "$ServerUrl")

  if [[ -n "$ServerApiKey" ]]; then
    echo "Registering Tentacle with API key"
    ARGS+=('--apiKey' $ServerApiKey)
  elif [[ -n "$BearerToken" ]]; then
    echo "Registering Tentacle with Bearer Token"
    ARGS+=('--bearerToken' "$BearerToken")
  else
    echo "Registering Tentacle with username/password"
    ARGS+=(
      '--username' "$ServerUsername"
      '--password' "$ServerPassword")
  fi

  ARGS+=('--server-comms-address' "$serverCommsAddress")

  tentacle "${ARGS[@]}"
}

function setPollingProxy() {
  local ARGS=()
  ARGS+=('polling-proxy'
    '--instance' "$instanceName")

  if [[ -n "$TentaclePollingProxyHost" ]]; then
    echo "Using polling proxy at $TentaclePollingProxyHost:$TentaclePollingProxyPort"
    ARGS+=(
      '--proxyEnable' 'true'
      '--proxyHost' "$TentaclePollingProxyHost"
      '--proxyPort' "$TentaclePollingProxyPort"
      '--proxyUsername' "$TentaclePollingProxyUsername"
      '--proxyPassword' "$TentaclePollingProxyPassword")
  else
    echo "Disabling polling proxy"
        ARGS+=('--proxyEnable' 'false')
  fi

  tentacle "${ARGS[@]}"
}

function markAsInitialised() {
    # There is a startupProbe which checks for this file
    mkdir -p /etc/octopus && touch /etc/octopus/initialized
}

setupVariablesForRegistrationCheck
getStatusOfRegistration

# We expect the tentacle to always be registered due to the new pre-install hook
# But keeping this here just in case
# We can remove it in future if we need to
if [ "$IS_REGISTERED" == "true" ]; then
  echo "Tentacle is already configured and registered with server."
  addAdditionalServerInstancesIfRequired
  setPollingProxy
else
  echo "==============================================="
  echo "Configuring Octopus Deploy Kubernetes Tentacle"

  validateVariables

  echo "==============================================="

  migrateFromPreinstallScript
  configureTentacle
  setPollingProxy
  registerTentacle
  addAdditionalServerInstancesIfRequired
  
  echo "Configuration successful"
fi

markAsInitialised

echo "==============================================="
echo "Starting Octopus Deploy Kubernetes Tentacle"
echo "==============================================="

# the exec here means that the host bash process is replaced by the tentacle process
exec tentacle agent --instance Tentacle --noninteractive
