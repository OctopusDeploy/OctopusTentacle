#!/bin/bash
set -eu

if [[ "$ACCEPT_EULA" != "Y" ]]; then
    echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
    exit 1
fi

# Tentacle Docker images only support once instance per container. Running multiple instances can be achieved by running multiple containers.
instanceName=Tentacle
configurationDirectory=/etc/octopus
applicationsDirectory=/home/Octopus/Applications
alreadyConfiguredSemaphore="$configurationDirectory/.configuredSemaphore"
internalListeningPort=10933

mkdir -p $configurationDirectory
mkdir -p $applicationsDirectory

# Setting ServerThumbprint configures a listening Tentacle that trusts that Octopus Server without registering with it,
# for when the Tentacle cannot reach the server. The Tentacle is then added in Octopus by hand.
function isUnregistered() {
    [[ -n "$ServerThumbprint" ]]
}

function printThumbprintForManualRegistration() {
    local thumbprint
    thumbprint=$(tentacle show-thumbprint --instance "$instanceName")
    echo "This Tentacle has not registered itself with Octopus Server."
    echo "Add it in Octopus as a listening Tentacle with thumbprint $thumbprint"
}

if [ -f "$alreadyConfiguredSemaphore" ]; then
    echo "Octopus Tentacle is already configured. Skipping reconfiguration."
    echo "If you want to force reconfiguration, please delete the file $alreadyConfiguredSemaphore and re-launch the container."
    if isUnregistered; then
        printThumbprintForManualRegistration
    fi
    exit 0
fi

function getPublicHostName() {
    if [[ "$PublicHostNameConfiguration" == "PublicIp" ]]; then
        curl https://api.ipify.org/
    elif [[ "$PublicHostNameConfiguration" == "FQDN" ]]; then
        hostname --fqdn
    elif [[ "$PublicHostNameConfiguration" == "ComputerName" ]]; then
        hostname
    else
        echo "$CustomPublicHostName"
    fi
}

function validateUnregisteredVariables() {
    if [[ ! "$ServerThumbprint" =~ ^[0-9A-Fa-f]{40}$ ]]; then
        echo "The 'ServerThumbprint' environment variable must be the 40 character hexadecimal thumbprint of the Octopus Server's certificate" >&2
        exit 1
    fi

    # These only affect registration or polling, so setting them alongside ServerThumbprint is a mistake. Variables
    # the image gives a default value, such as Space, can't be told apart from ones the user set, so aren't checked.
    local registrationVariables=(
        ServerUrl ServerApiKey BearerToken ServerUsername ServerPassword
        ServerCommsAddress ServerPort PollingConnectionCount AsKubernetesTentacle
        TargetEnvironment TargetRole TargetWorkerPool TargetName
        TargetTenant TargetTenantTag TargetTenantedDeploymentParticipation
        ListeningPort CustomPublicHostName ProxyName
    )
    local variable
    for variable in "${registrationVariables[@]}"; do
        if [[ -n "${!variable:-}" ]]; then
            echo "The '$variable' environment variable can't be used with 'ServerThumbprint': a Tentacle configured with 'ServerThumbprint' listens for the Octopus Server and doesn't register with it" >&2
            exit 1
        fi
    done

    echo " - communication mode 'Listening' (Passive), not registered with Octopus Server"
    echo " - trusted server thumbprint '$ServerThumbprint'"
}

function validateVariables() {
    if isUnregistered; then
        validateUnregisteredVariables
        return
    fi

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

    if [[ ! -z "$TargetWorkerPool" ]]; then
        if [[ ! -z "$TargetEnvironment" ]]; then
            echo "The 'TargetEnvironment' environment variable is not valid in combination with the 'TargetWorkerPool' variable" >&2
            exit 1
        fi

        if [[ ! -z "$TargetRole" ]]; then
            echo "The 'TargetRole' environment variable is not valid in combination with the 'TargetWorkerPool' variable" >&2
            exit 1
        fi
    else
        if [[ -z "$TargetEnvironment" ]]; then
            echo "Please specify an environment name with the 'TargetEnvironment' environment variable" >&2
            exit 1
        fi

        if [[ -z "$TargetRole" ]]; then
            echo "Please specify a role name with the 'TargetRole' environment variable" >&2
            exit 1
        fi
    fi

    echo " - server endpoint '$ServerUrl'"
    echo " - api key '##########'"
    if [[ ! -z "$ServerCommsAddress" || ! -z "$ServerPort" ]]; then
        if [[ ! -z $AsKubernetesTentacle ]]; then
            echo " - communication mode 'Kubernetes' (Polling)"
        else
            echo " - communication mode 'Polling' (Active)"
        fi
        if [[ ! -z "$ServerCommsAddress" ]]; then
            echo " - server comms address $ServerCommsAddress"
        fi
        if [[ ! -z "$ServerPort" ]]; then
            echo " - server port $ServerPort"
        fi
    else
        if [[ ! -z $AsKubernetesTentacle ]]; then
            echo " - communication mode 'Kubernetes' (Listening)"
        else
            echo " - communication mode 'Listening' (Passive)"
        fi
        echo " - registered port $ListeningPort"
        if [[ ! -z "$ProxyName" ]]; then
            echo " - proxy '$ProxyName'"
        fi
    fi
    if [[ ! -z "$TargetWorkerPool" ]]; then
        echo " - worker pool '$TargetWorkerPool'"
    else
        echo " - environment '$TargetEnvironment'"
        echo " - role '$TargetRole'"
    fi
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
        tentacle configure --instance "$instanceName" --port "$internalListeningPort" --noListen "False"
    fi

    echo "Updating trust ..."
    tentacle configure --instance "$instanceName" --reset-trust
    if isUnregistered; then
        # Registering trusts the server; without registration, trust the given thumbprint instead.
        tentacle configure --instance "$instanceName" --trust "$ServerThumbprint"
    fi

    if [[ ! -z "$PollingConnectionCount" ]]; then
        echo "Setting polling connection count ..."
        tentacle configure --instance "$instanceName" --pollingConnectionCount "$PollingConnectionCount"
    fi

    if [[ -n "$TentacleCertificateBase64" ]]; then
        echo "Importing certificate ..."
        tentacle import-certificate --instance "$instanceName" --from-base64 "$TentacleCertificateBase64"
    else
        echo "Creating certificate ..."
        tentacle new-certificate --instance "$instanceName" --if-blank
    fi
}

function registerTentacle() {
    echo "Registering with server ..."

    local ARGS=()

    if [[ ! -z "$TargetWorkerPool" ]]; then
        ARGS+=(
        'register-worker'
        '--force')

        IFS=',' read -ra WORKER_POOLS <<<"$TargetWorkerPool"
        for i in "${WORKER_POOLS[@]}"; do
            ARGS+=('--workerpool' "$i")
        done
    else
        if [[ ! -z "$AsKubernetesTentacle" ]]; then
            ARGS+=('register-k8s-cluster')
        else
            ARGS+=(
            'register-with'
            '--force')
        fi

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
    fi

    ARGS+=(
        '--instance' "$instanceName"
        '--server' "$ServerUrl"
        '--space' "$Space"
        '--policy' "$MachinePolicy")

    if [[ ! -z "$ServerCommsAddress" || ! -z "$ServerPort" ]]; then
        ARGS+=('--comms-style' 'TentacleActive')

        if [[ ! -z "$ServerCommsAddress" ]]; then
            ARGS+=('--server-comms-address' "$ServerCommsAddress")
        fi

        if [[ ! -z "$ServerPort" ]]; then
            ARGS+=('--server-comms-port' "$ServerPort")
        fi
    else
        ARGS+=(
            '--comms-style' 'TentaclePassive'
            '--publicHostName' "$(getPublicHostName)")

        if [[ ! -z "$ListeningPort" && "$ListeningPort" != "$internalListeningPort" ]]; then
            ARGS+=('--tentacle-comms-port' "$ListeningPort")
        fi
        if [[ ! -z "$ProxyName" ]]; then
            ARGS+=('--proxy' "$ProxyName")
        fi
    fi

    if [[ ! -z "$ServerApiKey" ]]; then
        echo "Registering Tentacle with API key"
        ARGS+=('--apiKey' "$ServerApiKey")
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
echo "Configuring Octopus Deploy Tentacle"

validateVariables

echo "==============================================="

configureTentacle
if isUnregistered; then
    printThumbprintForManualRegistration
else
    registerTentacle
fi

touch $alreadyConfiguredSemaphore

echo "Configuration successful."
echo ""
