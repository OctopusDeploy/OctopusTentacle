#!/bin/sh

/opt/octopus/tentacle/Tentacle create-instance --config /etc/octopus/default/tentacle-default.config
/opt/octopus/tentacle/Tentacle new-certificate --if-blank
/opt/octopus/tentacle/Tentacle configure --reset-trust
/opt/octopus/tentacle/Tentacle configure --app "/home/Octopus/Applications/"

echo ""
echo "To set up a listening tentacle:"
echo "    /opt/octopus/tentacle/Tentacle configure --port 10933 --noListen False"
echo "    /opt/octopus/tentacle/Tentacle configure --trust [SERVER_KEY]"
echo ""

sh /usr/share/pleaserun/Tentacle/install.sh