# From https://raw.githubusercontent.com/OctopusDeploy/BuildAgentAutomation/main/Common/shared/dotnet-install.sh
function install_dotnet {
    echo "downloading dotnet-install.sh so we can install dotnet $*"

    curl --silent --fail -L -O https://dot.net/v1/dotnet-install.sh || exit 1

    echo "marking script as executable"
    chmod +x dotnet-install.sh || exit 1

    # Parse the arguments with flags --runtime and --sdk
    while [ -n "$1" ]; do
        if [ "$1" == "--sdk" ]; then
          echo "Installing dotnet SDK $2"
          ./dotnet-install.sh --channel "$2" --verbose || exit 1
          shift
        elif [ "$1" == "--runtime" ]; then
          echo "Installing dotnet runtime $2"
          ./dotnet-install.sh --channel "$2" --runtime dotnet --verbose || exit 1
          shift
        else
          echo "Installing dotnet $1"
          ./dotnet-install.sh  --channel "$1" --verbose || exit 1
        fi
        shift
    done

    echo "Removing dotnet-install.sh script"
    rm -f dotnet-install.sh || exit 1
}