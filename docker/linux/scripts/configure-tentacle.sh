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
alreadyConfiguredSemaphore="$configurationDirectory/.configuredSemaphore"
internalListeningPort=10933

mkdir -p $configurationDirectory
mkdir -p $applicationsDirectory

if [ ! -f /usr/bin/tentacle ]; then
	ln -s /opt/octopus/tentacle/Tentacle /usr/bin/tentacle
fi

if [ -f "$alreadyConfiguredSemaphore" ]; then
    echo "Octopus Tentacle is already configured. Skipping reconfiguration."
    echo "If you want to force reconfiguration, please delete the file $alreadyConfiguredSemaphore and re-launch the container."
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
		echo $CustomPublicHostName
	fi
}

function validateVariables() {
	if [[ -z "$ServerApiKey" ]]; then
		if [[ -z "$ServerPassword" || -z "$ServerUsername" ]]; then
			echo "Please specify either an API key or a username/password with the 'ServerApiKey' or 'ServerUsername'/'ServerPassword' environment variables" >&2
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
  if [[ ! -z "$ServerPort" ]]; then
    echo " - communication mode 'Polling' (Active)"
    echo " - server port $ServerPort"
  else
    echo " - communication mode 'Listening' (Passive)"
    echo " - registered port $ListeningPort"
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
	if [[ ! -z "$ServerPort" ]]; then
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

	if [[ ! -z "$TargetWorkerPool" ]]; then
		ARGS+=('register-worker')

		IFS=',' read -ra WORKER_POOLS <<< "$TargetWorkerPool"
		for i in "${WORKER_POOLS[@]}"; do
			ARGS+=('--workerpool' "$i")
		done
	else
		ARGS+=('register-with')

		if [[ ! -z "$TargetEnvironment" ]]; then
			IFS=',' read -ra ENVIRONMENTS <<< "$TargetEnvironment"
			for i in "${ENVIRONMENTS[@]}"; do
				ARGS+=('--environment' "$i")
			done
		fi

		if [[ ! -z "$TargetRole" ]]; then
			IFS=',' read -ra ROLES <<< "$TargetRole"
			for i in "${ROLES[@]}"; do
				ARGS+=('--role' "$i")
			done
		fi

		if [[ ! -z "$TargetTenant" ]]; then
			IFS=',' read -ra TENANTS <<< "$TargetTenant"
			for i in "${TENANTS[@]}"; do
				ARGS+=('--tenant' "$i")
			done
		fi

		if [[ ! -z "$TargetTenantTag" ]]; then
			IFS=',' read -ra TENANTTAGS <<< "$TargetTenantTag"
			for i in "${TENANTTAGS[@]}"; do
				ARGS+=('--tenanttag' "$i")
			done
		fi
	fi

	ARGS+=(
		'--instance' "$instanceName"
		'--server' "$ServerUrl"
		'--space' "$Space"
		'--policy' "$MachinePolicy"
		'--force')

	if [[ ! -z "$ServerPort" ]]; then
		ARGS+=(
			'--comms-style' 'TentacleActive'
			'--server-comms-port' $ServerPort)
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

if [[ -z "${ConfigDir}" ]]; then
  echo "==============================================="
  echo "Configuring Octopus Deploy Tentacle"
  validateVariables
  echo "==============================================="
  configureTentacle
  registerTentacle
else
  echo "==============================================="
  echo "Configuring Octopus Deploy Tentacle"
  echo "==============================================="
  configureTentacle
  for filename in $(realpath ${CONFIG_DIR}/*.conf); do
    echo "processing $filename"
	. $filename
    validateVariables
    registerTentacle
  done  
fi

touch $alreadyConfiguredSemaphore

echo "Configuration successful."
echo ""
