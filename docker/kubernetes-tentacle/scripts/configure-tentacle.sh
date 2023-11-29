#!/bin/bash
set -e

if [[ "$ACCEPT_EULA" != "Y" ]]; then
    echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
    exit 1
fi

# Tentacle Docker images only support once instance per container. Running multiple instances can be achieved by running multiple containers.
instanceName=Tentacle
configurationDirectory=/etc/octopus
applicationsDirectory=/home/Octopus/Applications
internalListeningPort=10933

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

    if [[ ! -z "$ServerCommsAddress" || ! -z "$ServerPort" ]]; then
        echo " - communication mode 'Kubernetes' (Polling)"

        if [[ ! -z "$ServerCommsAddress" ]]; then
            echo " - server comms address $ServerCommsAddress"
        fi
        if [[ ! -z "$ServerPort" ]]; then
            echo " - server port $ServerPort"
        fi
    else
        echo " - communication mode 'Kubernetes' (Listening)"
        echo " - registered port $ListeningPort"
    fi

    echo " - environment '$TargetEnvironment'"
    echo " - role '$TargetRole'"
    echo " - host '$PublicHostNameConfiguration'"

    if [[ ! -z "$TargetName" ]]; then
        echo " - name '$TargetName'"
    fi
    if [[ ! -z "$TargetTenant" ]]; then
        echo " - tenant '$TargetTenant'"
    fi
    if [[ ! -z "$TargetTenantTag" ]]; then
        echo " - tenant tag '$TargetTenantTag'"
    fi
    if [[ ! -z "$TargetTenantedDeploymentParticipation" ]]; then
        echo " - tenanted deployment participation '$TargetTenantedDeploymentParticipation'"
    fi
    if [[ ! -z "$Space" ]]; then
        echo " - space '$Space'"
    fi
}

function configureTentacle() {
    tentacle create-instance --instance "$instanceName" --config "$configurationDirectory/tentacle.config"

    echo "Setting directory paths ..."
    tentacle configure --instance "$instanceName" --app "$applicationsDirectory"

    echo "Configuring communication type ..."
    if [[ ! -z "$ServerCommsAddress" || ! -z "$ServerPort" ]]; then
        tentacle configure --instance "$instanceName" --noListen "True"
    else
        tentacle configure --instance "$instanceName" --port $internalListeningPort --noListen "False"
    fi

    echo "Updating trust ..."
    tentacle configure --instance "$instanceName" --reset-trust

    echo "Creating certificate ..."
    tentacle new-certificate --instance "$instanceName" --if-blank
}

function registerTentacle() {
    echo "Registering with server ..."

    local ARGS=()

    ARGS+=('register-k8s-cluster')

    if [[ ! -z "$TargetEnvironment" ]]; then
        IFS=',' read -ra ENVIRONMENTS <<<"$TargetEnvironment"
        for i in "${ENVIRONMENTS[@]}"; do
            ARGS+=('--environment' "$i")
        done
    fi

    if [[ ! -z "$TargetRole" ]]; then
        IFS=',' read -ra ROLES <<<"$TargetRole"
        for i in "${ROLES[@]}"; do
            ARGS+=('--role' "$i")
        done
    fi

    if [[ ! -z "$TargetTenant" ]]; then
        IFS=',' read -ra TENANTS <<<"$TargetTenant"
        for i in "${TENANTS[@]}"; do
            ARGS+=('--tenant' "$i")
        done
    fi

    if [[ ! -z "$TargetTenantTag" ]]; then
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

    if [[ ! -z "$ServerCommsAddress" || ! -z "$ServerPort" ]]; then
        ARGS+=('--comms-style' 'TentacleActive')

        if [[ ! -z "$ServerCommsAddress" ]]; then
            ARGS+=('--server-comms-address' $ServerCommsAddress)
        fi

        if [[ ! -z "$ServerPort" ]]; then
            ARGS+=('--server-comms-port' $ServerPort)
        fi
    else
        ARGS+=(
            '--comms-style' 'TentaclePassive'
            '--publicHostName' $(getPublicHostName))

        if [[ ! -z "$ListeningPort" && "$ListeningPort" != "$internalListeningPort" ]]; then
            ARGS+=('--tentacle-comms-port' $ListeningPort)
        fi
    fi

    if [[ ! -z "$ServerApiKey" ]]; then
        echo "Registering Tentacle with API key"
        ARGS+=('--apiKey' $ServerApiKey)
    elif [[ ! -z "$BearerToken" ]]; then
        echo "Registering Tentacle with Bearer Token"
        ARGS+=('--bearerToken' "$BearerToken")
    else
        echo "Registering Tentacle with username/password"
        ARGS+=(
            '--username' "$ServerUsername"
            '--password' "$ServerPassword")
    fi

    if [[ ! -z "$TargetName" ]]; then
        ARGS+=('--name' "$TargetName")
    fi

    if [[ ! -z "$TargetTenantedDeploymentParticipation" ]]; then
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