# From https://raw.githubusercontent.com/OctopusDeploy/BuildAgentAutomation/main/Common/shared/dotnet-install.sh
function install_dotnet {
    echo "downloading dotnet-install.sh so we can install dotnet $1"

    curl --silent --fail -L -O https://dot.net/v1/dotnet-install.sh || exit 1

    echo "marking script as executable"
    chmod +x dotnet-install.sh || exit 1

    echo "Installing dotnet $1"
    sudo ./dotnet-install.sh --install-dir /usr/share/dotnet --channel $1 --verbose || exit 1

    echo "Removing dotnet-install.sh script"
    rm -f dotnet-install.sh || exit 1
}
