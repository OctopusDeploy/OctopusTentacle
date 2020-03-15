#!/bin/bash

GREEN='\033[1;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

function exitIfCommandFailed {
    if [ $? -ne 0 ]; then
        exit 1
    fi
}

function assignNonEmptyValue {
    if [ ! -z "$1" ]
    then
        echo $1
    else
        echo $2
    fi
}

function sanitizeName {
    #Remove some special characters
    echo ${1//[\'\"$^&]/}
}

function splitAndGetArgs {
    finalstring=""
    IFS=','
    #Convert string to array
    read -ra strarr <<< "$2"
    for i in "${strarr[@]}"; do
        finalstring+="--$1 \"$(echo $i | xargs)\" "
    done
    echo $finalstring
}

function showFinishedMessage {
    echo
    echo -e "${GREEN}Tentacle instance '$1' is now installed${NC}"
    echo
}

function setupListeningTentacle {
    instancename=$1
    logpath="/etc/octopus"
    applicationpath="/home/Octopus/Applications"
    port="10933"
    
    read -p "Where would you like Tentacle to store log files? ($logpath):" inputlogpath
    logpath=$(assignNonEmptyValue $inputlogpath $logpath)
    
    read -p "Where would you like Tentacle to install appications to? ($applicationpath):" inputapplicationpath
    applicationpath=$(assignNonEmptyValue $inputapplicationpath $applicationpath)

    read -p "Enter the port that this Tentacle will listen on ($port):" inputport
    port=$(assignNonEmptyValue $inputport $port)

    while [ -z "$thumbprint" ] 
    do
        read -p 'Enter the thumbprint of the Octopus Server: ' thumbprint
    done 

    echo -e "${GREEN}The following configuration commands will be run to configure Tentacle:"

    echo -e "${YELLOW}sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\""
    echo "sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank"
    echo "sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --port $port --noListen False --reset-trust"
    echo -e "sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --trust $thumbprint"

    echo -e "sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"${NC}"

    read -p "Press enter to continue..."
    
    eval sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\"
    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank
    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --port $port --noListen False --reset-trust
    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --trust $thumbprint
    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"
    exitIfCommandFailed

    showFinishedMessage $instancename
}

function setupPollingTentacle {
    instancename=$1
    displayname=$(hostname)
    logpath="/etc/octopus"
    applicationpath="/home/Octopus/Applications"
    space="Default"
    envstring=""
    rolesstring=""
    workerpoolsstring=""
    machinetype=1

    read -p "Where would you like Tentacle to store log files? ($logpath):" inputlogpath
    logpath=$(assignNonEmptyValue $inputlogpath $logpath)
    
    read -p "Where would you like Tentacle to install appications to? ($applicationpath):" inputapplicationpath
    applicationpath=$(assignNonEmptyValue $inputapplicationpath $applicationpath)

    while [ -z "$octopusserverurl" ] 
    do
        read -p 'Octopus Server URL (eg. https://octopus-server): ' octopusserverurl
    done 

    read -p 'Select auth method: 1) API-Key or 2) Username and Password (default 1): ' authmethod

    case $authmethod in
        2)
            while [ -z "$username" ] 
            do
                read -p 'Username: ' username
            done
            while [ -z "$password" ] 
            do
                read -s -p 'Password: ' password
                echo
            done 
            auth="--username \"$username\" --password \"$password\""
            displayauth="--username \"$username\" --password \"**********\""
            ;; 
        *)
            while [ -z "$apikey" ] 
            do
                read -s -p 'API-Key: ' apikey
            done 
            auth="--apiKey \"$apikey\""
            displayauth="--apiKey \"API-XXXXXXXXXXXXXXXXXXXXXXXXXX\""
            ;;
    esac

    read -p "Select type of Tentacle do you want to setup: 1) Deployment Target or 2) Worker (default $machinetype): " inputmachinetype
    machinetype=$(assignNonEmptyValue $inputmachinetype $machinetype)

    read -p 'What Space would you like to register this Tentacle in? (Default): ' spaceinput
    space=$(assignNonEmptyValue $spaceinput $space)

    read -p "What name would you like to register this Tentacle with? ($displayname): " displaynameinput
    displayname=$(assignNonEmptyValue $displaynameinput $displayname)

    case $machinetype in
        2)
            #Get worker pools
            while [ -z "$workerpoolsinput" ] 
            do
                read -p 'Enter the worker pools for this Tentacle (comma seperated): ' workerpoolsinput
            done 
            workerpoolsstring=$(splitAndGetArgs "workerpool" "$workerpoolsinput")
            ;; 
        *)
            #Get environments
            while [ -z "$environmentsinput" ] 
            do
                read -p 'Enter the environments for this Tentacle (comma seperated): ' environmentsinput
            done 
            envstring=$(splitAndGetArgs "environment" "$environmentsinput")

            #Get roles
            while [ -z "$rolesinput" ] 
            do
                read -p 'Enter the roles for this Tentacle (comma seperated): ' rolesinput
            done
            rolesstring=$(splitAndGetArgs "role" "$rolesinput")
            ;;
    esac

    echo -e "${GREEN}The following configuration commands will be run to configure Tentacle:"
    echo -e "${YELLOW}sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\""
    echo -e "sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank"
    echo -e "sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --noListen \"True\" --reset-trust"

    if [ $machinetype = 2 ]; then
        echo -e "sudo /opt/octopus/tentacle/Tentacle register-worker --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" --server-comms-port \"10943\" $displayauth --space \"$space\" $workerpoolsstring"
    else
        echo -e "sudo /opt/octopus/tentacle/Tentacle register-with --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" --server-comms-port \"10943\" $displayauth --space \"$space\" $envstring $rolesstring"
    fi

    echo -e "sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"${NC}"

    read -p "Press enter to continue..."
    
    eval sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\"
    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank
    exitIfCommandFailed 

    eval sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --noListen \"True\" --reset-trust
    exitIfCommandFailed 

    if [ $machinetype = 2 ]; then
        eval sudo /opt/octopus/tentacle/Tentacle register-worker --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" --server-comms-port \"10943\" $auth --space \"$space\" $workerpoolsstring
    else
        eval sudo /opt/octopus/tentacle/Tentacle register-with --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" --server-comms-port \"10943\" $auth --space \"$space\" $envstring $rolesstring
    fi

    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"
    exitIfCommandFailed

    showFinishedMessage $instancename
}

instance="Tentacle"

read -p "Name of Tentacle instance (default $instance):" inputinstance
instance=$(assignNonEmptyValue $(sanitizeName $inputinstance) $instance)

if [ "$instance" != "$inputinstance" ]
then
    echo -e "${YELLOW}Invalid characters will be ignored, the instance name will be: '${instance}'"
    echo -e "${NC}"
fi

read -p 'What kind of Tentacle would you like to configure: 1) Listening or 2) Polling (default 1): ' commsstlye

case $commsstlye in
     2)
          setupPollingTentacle $instance
          ;; 
     *)
          setupListeningTentacle $instance
          ;;
esac