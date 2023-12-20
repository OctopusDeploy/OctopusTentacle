#!/bin/bash
set -e

if [[ "$ACCEPT_EULA" != "Y" ]]; then
    echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
    exit 1
fi

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

    if [[ -z "$TargetEnvironment" ]]; then
        echo "Please specify one or more environment names (comma delimited) with the 'TargetEnvironment' environment variable" >&2
        exit 1
    fi

    if [[ -z "$TargetRole" ]]; then
        echo "Please specify one or more role names (comma delimited) with the 'TargetRole' environment variable" >&2
        exit 1
    fi

    echo " - server endpoint '$ServerUrl'"
    echo " - api key '##########'"

    if [[ -n "$ServerCommsAddress" || -n "$ServerPort" ]]; then
        echo " - communication mode 'Kubernetes' (Polling)"

        if [[ -n "$ServerCommsAddress" ]]; then
            echo " - server comms address $ServerCommsAddress"
        fi
        if [[ -n "$ServerPort" ]]; then
            echo " - server port $ServerPort"
        fi
    else
        echo " - communication mode 'Kubernetes' (Listening)"
        echo " - registered port $ListeningPort"
    fi

    echo " - environment '$TargetEnvironment'"
    echo " - role '$TargetRole'"
    echo " - host '$PublicHostNameConfiguration'"

    if [[ -n "$TargetName" ]]; then
        echo " - name '$TargetName'"
    fi
    if [[ -n "$TargetTenant" ]]; then
        echo " - tenant '$TargetTenant'"
    fi
    if [[ -n "$TargetTenantTag" ]]; then
        echo " - tenant tag '$TargetTenantTag'"
    fi
    if [[ -n "$TargetTenantedDeploymentParticipation" ]]; then
        echo " - tenanted deployment participation '$TargetTenantedDeploymentParticipation'"
    fi
    if [[ -n "$Space" ]]; then
        echo " - space '$Space'"
    fi
}

function configureTentacle() {
    tentacle create-instance --instance "$instanceName" --config "$configurationDirectory/tentacle.config" --home "$configurationDirectory"

    echo "Setting directory paths ..."
    tentacle configure --instance "$instanceName" --app "$applicationsDirectory"

    echo "Configuring communication type ..."
    if [[ -n "$ServerCommsAddress" || -n "$ServerPort" ]]; then
        tentacle configure --instance "$instanceName" --noListen "True"
    else
        tentacle configure --instance "$instanceName" --port $internalListeningPort --noListen "False"
    fi

    echo "Updating trust ..."
    tentacle configure --instance "$instanceName" --reset-trust

    echo "Creating certificate ..."
    tentacle new-certificate --instance "$instanceName" --if-blank
}

function setupVariablesForRegistrationCheck() {
    local namespace=$(cat /var/run/secrets/kubernetes.io/serviceaccount/namespace)
    local config_map_name="TentacleConfigMap"
    SERVICE_URL="https://$KUBERNETES_SERVICE_HOST:$KUBERNETES_SERVICE_PORT/api/v1/namespaces/$namespace/configmaps/$config_map_name";    
    SERVICE_ACCOUNT_TOKEN_PATH="/var/run/secrets/kubernetes.io/serviceaccount/token"
}

function getStatusOfRegistration() {
    echo "Checking registration status..."

    IS_REGISTERED=$(curl -s --request GET \
    --url "$SERVICE_URL" \
    --header "Authorization: Bearer $(cat $SERVICE_ACCOUNT_TOKEN_PATH)" \
    --header 'Accept: application/json' \
    --cacert /var/run/secrets/kubernetes.io/serviceaccount/ca.crt \
    | grep -o '"is_registered": "[^"]*"' \
    | cut -d'"' -f4)
}

function setStatusAsRegistered() {    
    echo "Storing registration status..."

    local json_patch='{"data":{"is_registered":"true"}}'

    curl --request PATCH \
        --url "$SERVICE_URL" \
        --header "Authorization: Bearer $(cat $SERVICE_ACCOUNT_TOKEN_PATH)" \
        --header 'Content-Type: application/merge-patch+json' \
        --data-raw "$json_patch" \
        --cacert /var/run/secrets/kubernetes.io/serviceaccount/ca.crt
}

function registerTentacle() {
    setupEnvironmentVariablesForRegistrationCheck
    getStatusOfRegistration
    
    if [ "$IS_REGISTERED" == "true" ]; then
        echo "Tentacle is already registered with server."
        return 0
    else 
        echo "Tentacle is not yet registered with server."
    fi    

    echo "Registering with server..."

    local ARGS=()

    ARGS+=('register-k8s-cluster')

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

    ARGS+=(
        '--instance' "$instanceName"
        '--server' "$ServerUrl"
        '--space' "$Space"
        '--policy' "$MachinePolicy")

    if [[ -n "$ServerCommsAddress" || -n "$ServerPort" ]]; then
        ARGS+=('--comms-style' 'TentacleActive')

        if [[ -n "$ServerCommsAddress" ]]; then
            ARGS+=('--server-comms-address' $ServerCommsAddress)
        fi

        if [[ -n "$ServerPort" ]]; then
            ARGS+=('--server-comms-port' $ServerPort)
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

    if [[ -n "$TargetName" ]]; then
        ARGS+=('--name' "$TargetName")
    fi

    if [[ -n "$TargetTenantedDeploymentParticipation" ]]; then
        ARGS+=('--tenanted-deployment-participation' "$TargetTenantedDeploymentParticipation")
    fi

    tentacle "${ARGS[@]}"
}

echo "==============================================="
echo "Configuring Octopus Deploy Kubernetes Tentacle"

validateVariables

echo "==============================================="

configureTentacle
registerTentacle

echo "Configuration successful."
echo ""