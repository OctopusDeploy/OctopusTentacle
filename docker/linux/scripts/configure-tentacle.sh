#!/bin/bash
set -eux

# This is very much a work in progress. Please do not view this as production-ready. It's in Octopus-internal use at
# the moment and will be made more robust in due course.

function getPublicHostName() {
	if [[ $PublicHostNameConfiguration == 'PublicIp' ]]; then
		curl https://api.ipify.org/
	elif [[ $PublicHostNameConfiguration == 'FQDN' ]]; then
		hostname --fqdn
	elif [[ $PublicHostNameConfiguration == 'ComputerName' ]]; then
		hostname
	else
		echo $CustomPublicHostName
	fi
}

if [[ "$ACCEPT_EULA" != "Y" ]]; then
    echo "ERROR: You must accept the EULA at https://octopus.com/company/legal by passing an environment variable 'ACCEPT_EULA=Y'"
    exit 1
fi

if [ -f "/usr/bin/tentacle" ]; then
    echo "Octopus Tentacle is already configured."
    return
fi

ln -s /opt/octopus/tentacle/Tentacle /usr/bin/tentacle

# Tentacle Docker images only support once instance per container. Running multiple instances can be achieved by running multiple containers.
instanceName=Tentacle
configurationDirectory=/etc/octopus
applicationsDirectory=/home/Octopus/Applications

mkdir -p $configurationDirectory
mkdir -p $applicationsDirectory

ARGS=()

if [[ ! -z "$WorkerPool" ]]; then
	ARGS+=('register-worker')
			
	IFS=',' read -ra WORKER_POOLS <<< "$WorkerPool"
	for i in "${WORKER_POOLS[@]}"; do
		ARGS+=('--workerpool' $i)
	done
else
	ARGS+=('register-with')
	  
	if [[ ! -z "$TargetEnvironment" ]]; then
		IFS=',' read -ra ENVIRONMENTS <<< "$TargetEnvironment"
		for i in "${ENVIRONMENTS[@]}"; do
			ARGS+=('--environment' $i)
		done
	fi
	
	if [[ ! -z "$TargetRole" ]]; then
		IFS=',' read -ra ROLES <<< "$TargetRole"
		for i in "${ROLES[@]}"; do
			ARGS+=('--role' $i)
		done
	fi
fi

ARGS+=(
	'--console'
	'--instance' $instanceName
	'--server' $ServerUrl
	'--force')
		
if [[ ! -z "$ServerPort" ]]; then
	ARGS+=(
		'--server-comms-port' $ServerPort)
else
	ARGS+=(
		'--comms-style' 'TentaclePassive'
		'--publicHostName' $(getPublicHostName)
		'--tentacle-comms-port' $ListeningPort)
fi

if [[ ! -z "$ServerApiKey" ]]; then
	ARGS+=('--apiKey' $ServerApiKey)
else
	ARGS+=(
		'--username' $ServerUsername
    	'--password' $ServerPassword)
fi

if [[ ! -z "$TargetName" ]]; then
	ARGS+=('--name' $TargetName)
fi

ARGS+=('--force')

tentacle create-instance --instance "$instanceName" --config "$configurationDirectory/tentacle.config"
tentacle new-certificate --instance "$instanceName" --if-blank
tentacle configure --instance "$instanceName" --app "$applicationsDirectory" --noListen "True" --reset-trust

tentacle "${ARGS[@]}"

#tentacle register-worker --instance "$instanceName" --server "$ServerUrl" --name "$HOSTNAME" --comms-style "$CommunicationsStype" --server-comms-port $ServerPort --apiKey $ServerApiKey --space "$Space" --workerpool="$WorkerPool" --policy="$MachinePolicy" --force
