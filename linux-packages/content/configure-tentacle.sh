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

function get_proxy_details {
    read -p "Enter the proxy Host (eg. http://proxyserver): " proxyurl
    proxyarg+=" --proxyHost=\"$proxyurl\""
    read -p "Enter the proxy Port (eg. 8080): " proxyport
    if [ -n "$proxyport" ]; then
        proxyarg+=" --proxyPort=\"$proxyport\""
    fi
    read -p "Enter the proxy Username (leave blank for none): " proxyusername
    if [ -n "$proxyusername" ]; then
        proxyarg+=" --proxyUsername=\"$proxyusername\""
        read -sp "Enter the proxy Password: " proxypassword
        if [ -n "$proxypassword" ]; then
            proxyarg+=" --proxyPassword=\"$proxypassword\""
        fi
    fi

    echo $proxyarg
}

function setupListeningTentacle {
    instancename=$@
    logpath="/etc/octopus"
    applicationpath="/home/Octopus/Applications"
    port="10933"
    
    read -p "Where would you like Tentacle to store configuration, logs, and working files? ($logpath):" inputlogpath
    logpath=$(assignNonEmptyValue "$inputlogpath" $logpath)
    
    read -p "Where would you like Tentacle to install applications to? ($applicationpath):" inputapplicationpath
    applicationpath=$(assignNonEmptyValue "$inputapplicationpath" $applicationpath)

    read -p "Enter the port that this Tentacle will listen on ($port):" inputport
    port=$(assignNonEmptyValue "$inputport" $port)

    read -p "Should the Tentacle use a proxy to communicate with Octopus? (y/N): " useproxy
    case "${useproxy,,}" in
        y | yes)
            proxyarg="$(get_proxy_details)"
            # Mask the proxy password in the display string
            proxyargdisplay=$(echo -n "$proxyarg" | sed 's/proxyPassword=.*/proxyPassword=\"**********\"/')
            # Added newline if password is present to improve readability
            if [[ $proxyargdisplay == *"proxyPassword="* ]]; then
                echo
            fi
            ;;
        *)
            proxyarg=""
            ;;
    esac

    while [ -z "$thumbprint" ] 
    do
        read -p 'Enter the thumbprint of the Octopus Server: ' thumbprint
    done 

    echo -e "${GREEN}The following configuration commands will be run to configure Tentacle:"

    echo -e "${YELLOW}sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\""
    echo "sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank"
    echo "sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --port $port --noListen False --reset-trust"
    echo -e "sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --trust $thumbprint"
    if [ -n "$proxyarg" ]; then
        echo -e "sudo /opt/octopus/tentacle/Tentacle proxy --instance \"$instancename\" --proxyEnable=\"true\" $proxyargdisplay"
    fi

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

    if [ -n "$proxyarg" ]; then
        eval sudo /opt/octopus/tentacle/Tentacle proxy --instance \"$instancename\" --proxyEnable=\"true\" $proxyarg
        exitIfCommandFailed
    fi

    eval sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"
    exitIfCommandFailed

    showFinishedMessage $instancename
}

function setupPollingTentacle {
    instancename=$@
    displayname=$(hostname)
    logpath="/etc/octopus"
    applicationpath="/home/Octopus/Applications"
    space="Default"
    envstring=""
    rolesstring=""
    workerpoolsstring=""
    machinetype=1
    servercommsport=10943

    read -p "Where would you like Tentacle to store log files? ($logpath):" inputlogpath
    logpath=$(assignNonEmptyValue "$inputlogpath" $logpath)
    
    read -p "Where would you like Tentacle to install applications to? ($applicationpath):" inputapplicationpath
    applicationpath=$(assignNonEmptyValue "$inputapplicationpath" $applicationpath)

    while [ -z "$octopusserverurl" ] 
    do
        read -p 'Octopus Server URL (eg. https://octopus-server): ' octopusserverurl
    done 

    read -p 'Select auth method: 1) API-Key or 2) Username and Password (default 1): ' authmethod

    case "${authmethod,,}" in
        2 | username | "username and password")
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
                echo
            done 
            auth="--apiKey \"$apikey\""
            displayauth="--apiKey \"API-XXXXXXXXXXXXXXXXXXXXXXXXXX\""
            ;;
    esac

    read -p "Select type of Tentacle do you want to setup: 1) Deployment Target or 2) Worker (default $machinetype): " inputmachinetype
    machinetype=$(assignNonEmptyValue "$inputmachinetype" $machinetype)
	machinetype="${machinetype,,}"

    read -p 'What Space would you like to register this Tentacle in? (Default): ' spaceinput
    space=$(assignNonEmptyValue "$spaceinput" $space)

    read -p "What name would you like to register this Tentacle with? ($displayname): " displaynameinput
    displayname=$(assignNonEmptyValue "$displaynameinput" $displayname)

    $doesconnecttosamedomain=1
    read -p 'Is the comms port on the same domain as the Octopus Server : 1) Yes or 2) No (default 1): ' doesconnecttosamedomain

    
    
    case $doesconnecttosamedomain in
        2)
            read -p "What is the Octopus Server comms address including port e.g. 'https://polling.<yoururl>.octopus.app:443' ?: " commsAddress
            commsAddressOrPortArgs="--server-comms-address \"$commsAddress\""
            ;;
            
        *)
            read -p "What port is the Octopus Server comms port? ($servercommsport): " servercommsportinput
            servercommsport=$(assignNonEmptyValue "$servercommsportinput" $servercommsport)

            commsAddressOrPortArgs="--server-comms-port \"$servercommsport\""
            ;;
    esac

    case $machinetype in
        2 | worker)
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

    read -p "Should the Tentacle use a proxy to communicate with Octopus? (y/N): " useproxy
    case "${useproxy,,}" in
        y | yes)
            proxyarg="$(get_proxy_details)"
            # Mask the proxy password in the display string
            proxyargdisplay=$(echo -n "$proxyarg" | sed 's/proxyPassword=.*/proxyPassword=\"**********\"/')
            # Added newline if password is present to improve readability
            if [[ $proxyargdisplay == *"proxyPassword="* ]]; then
                echo
            fi
            ;;
        *)
            proxyarg=""
            ;;
    esac

    echo -e "${GREEN}The following configuration commands will be run to configure Tentacle:"
    echo -e "${YELLOW}sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\""
    echo -e "sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank"
    echo -e "sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --noListen \"True\" --reset-trust"

    if [ $machinetype = 2 ] || [ $machinetype = "worker" ]; then
        echo -e "sudo /opt/octopus/tentacle/Tentacle register-worker --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" $commsAddressOrPortArgs $displayauth --space \"$space\" $workerpoolsstring"
    else
        echo -e "sudo /opt/octopus/tentacle/Tentacle register-with --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" $commsAddressOrPortArgs $displayauth --space \"$space\" $envstring $rolesstring"
    fi

    if [ -n "$proxyarg" ]; then
        echo -e "sudo /opt/octopus/tentacle/Tentacle polling-proxy --instance \"$instancename\" --proxyEnable=\"true\" $proxyargdisplay"
    fi

    echo -e "sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"${NC}"

    read -p "Press enter to continue..."
    
    eval sudo /opt/octopus/tentacle/Tentacle create-instance --instance \"$instancename\" --config \"$logpath/$instancename/tentacle-$instancename.config\"
    exitIfCommandFailed

    eval sudo /opt/octopus/tentacle/Tentacle new-certificate --instance \"$instancename\" --if-blank
    exitIfCommandFailed 

    eval sudo /opt/octopus/tentacle/Tentacle configure --instance \"$instancename\" --app \"$applicationpath\" --noListen \"True\" --reset-trust
    exitIfCommandFailed 

    if [ $machinetype = 2 ] || [ $machinetype = "worker" ]; then
        eval sudo /opt/octopus/tentacle/Tentacle register-worker --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" $commsAddressOrPortArgs $auth --space \"$space\" $workerpoolsstring
    else
        eval sudo /opt/octopus/tentacle/Tentacle register-with --instance \"$instancename\" --server \"$octopusserverurl\" --name \"$displayname\" --comms-style \"TentacleActive\" $commsAddressOrPortArgs $auth --space \"$space\" $envstring $rolesstring
    fi
    exitIfCommandFailed

    if [ -n "$proxyarg" ]; then
        eval sudo /opt/octopus/tentacle/Tentacle polling-proxy --instance \"$instancename\" --proxyEnable=\"true\" $proxyarg
        exitIfCommandFailed
    fi

    eval sudo /opt/octopus/tentacle/Tentacle service --install --start --instance \"$instancename\"
    exitIfCommandFailed

    showFinishedMessage $instancename
}

instance="Tentacle"

read -p "Name of Tentacle instance (default $instance):" inputinstance
instance=$(assignNonEmptyValue "$(sanitizeName "$inputinstance")" $instance)

if [ "$instance" != "$inputinstance" ]
then
    echo -e "${YELLOW}Invalid characters will be ignored, the instance name will be: '${instance}'"
    echo -e "${NC}"
fi

read -p 'What kind of Tentacle would you like to configure: 1) Listening or 2) Polling (default 1): ' commsstlye

case "${commsstlye,,}"  in
     2 | polling)
          setupPollingTentacle $instance
          ;; 
     *)
          setupListeningTentacle $instance
          ;;
esac
