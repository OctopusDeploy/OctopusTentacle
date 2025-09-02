#!/bin/bash

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENV_FILE="$HOME/.octopus-tentacle-intercept.env"

# Function to print colored output
print_status() {
    echo -e "${GREEN}[]${NC} $1"
}

print_error() {
    echo -e "${RED}[]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

print_info() {
    echo -e "${BLUE}[i]${NC} $1"
}

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to install telepresence
install_telepresence() {
    print_warning "Telepresence is not installed. Installing..."
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS - detect architecture
        ARCH=$(uname -m)
        if [[ "$ARCH" == "arm64" ]]; then
            # Apple Silicon
            print_info "Detected Apple Silicon Mac (arm64)"
            sudo curl -fL https://github.com/telepresenceio/telepresence/releases/latest/download/telepresence-darwin-arm64 -o /usr/local/bin/telepresence
        else
            # Intel Mac
            print_info "Detected Intel Mac (amd64)"
            sudo curl -fL https://github.com/telepresenceio/telepresence/releases/latest/download/telepresence-darwin-amd64 -o /usr/local/bin/telepresence
        fi
        sudo chmod a+x /usr/local/bin/telepresence
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux - detect architecture
        ARCH=$(uname -m)
        if [[ "$ARCH" == "aarch64" ]] || [[ "$ARCH" == "arm64" ]]; then
            # ARM64
            print_info "Detected ARM64 Linux"
            sudo curl -fL https://github.com/telepresenceio/telepresence/releases/latest/download/telepresence-linux-arm64 -o /usr/local/bin/telepresence
        else
            # AMD64
            print_info "Detected AMD64 Linux"
            sudo curl -fL https://github.com/telepresenceio/telepresence/releases/latest/download/telepresence-linux-amd64 -o /usr/local/bin/telepresence
        fi
        sudo chmod a+x /usr/local/bin/telepresence
    else
        print_error "Unsupported OS: $OSTYPE"
        exit 1
    fi
    
    print_status "Telepresence installed successfully"
}

# Function to install kubectx/kubens
install_kubectx() {
    print_warning "kubectx/kubens is not installed. Installing via go..."
    
    # Check if go is installed
    if ! command_exists go; then
        print_error "Go is not installed. Please install Go first: https://golang.org/doc/install"
        exit 1
    fi
    
    print_info "Installing kubectx and kubens using go install..."
    
    # Install kubectx
    go install github.com/ahmetb/kubectx/cmd/kubectx@latest
    
    # Install kubens
    go install github.com/ahmetb/kubectx/cmd/kubens@latest
    
    # Check if GOPATH/bin is in PATH
    GOBIN=$(go env GOPATH)/bin
    if [[ ":$PATH:" != *":$GOBIN:"* ]]; then
        print_warning "GOPATH/bin is not in your PATH. Adding it for this session..."
        export PATH="$GOBIN:$PATH"
        print_info "Consider adding 'export PATH=\"\$PATH:$GOBIN\"' to your shell profile"
    fi
    
    print_status "kubectx and kubens installed successfully"
}

# Function to install fzf
install_fzf() {
    print_warning "fzf is not installed. Installing..."
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        if command_exists brew; then
            brew install fzf
        else
            # Install using git
            git clone --depth 1 https://github.com/junegunn/fzf.git ~/.fzf
            ~/.fzf/install --bin
        fi
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux - install using git
        git clone --depth 1 https://github.com/junegunn/fzf.git ~/.fzf
        ~/.fzf/install --bin
    else
        print_error "Unsupported OS: $OSTYPE"
        exit 1
    fi
    
    print_status "fzf installed successfully"
}

# Function to install jq
install_jq() {
    print_warning "jq is not installed. Installing..."
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        if command_exists brew; then
            brew install jq
        else
            # Download binary directly
            ARCH=$(uname -m)
            if [[ "$ARCH" == "arm64" ]]; then
                sudo curl -fL https://github.com/jqlang/jq/releases/latest/download/jq-macos-arm64 -o /usr/local/bin/jq
            else
                sudo curl -fL https://github.com/jqlang/jq/releases/latest/download/jq-macos-amd64 -o /usr/local/bin/jq
            fi
            sudo chmod +x /usr/local/bin/jq
        fi
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux
        sudo curl -fL https://github.com/jqlang/jq/releases/latest/download/jq-linux-amd64 -o /usr/local/bin/jq
        sudo chmod +x /usr/local/bin/jq
    else
        print_error "Unsupported OS: $OSTYPE"
        exit 1
    fi
    
    print_status "jq installed successfully"
}

# Check prerequisites
print_info "Checking prerequisites..."

if ! command_exists kubectl; then
    print_error "kubectl is not installed. Please install it first."
    exit 1
fi
print_status "kubectl is installed"

if ! command_exists telepresence; then
    install_telepresence
fi
print_status "telepresence is installed"

if ! command_exists kubectx || ! command_exists kubens; then
    install_kubectx
fi
print_status "kubectx/kubens is installed"

if ! command_exists fzf; then
    install_fzf
fi
print_status "fzf is installed"

if ! command_exists jq; then
    install_jq
fi
print_status "jq is installed"

# Loop until user confirms context and namespace
while true; do
    # Get current context and namespace
    CURRENT_CONTEXT=$(kubectl config current-context 2>/dev/null || echo "none")
    CURRENT_NAMESPACE=$(kubectl config view --minify --output 'jsonpath={..namespace}' 2>/dev/null || echo "default")
    
    echo ""
    print_info "Current context: $CURRENT_CONTEXT"
    print_info "Current namespace: $CURRENT_NAMESPACE"
    
    read -p "Are these the correct settings? (y/n): " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        print_status "Continuing with context $CURRENT_CONTEXT and namespace $CURRENT_NAMESPACE"
        break
    else
        # Select Kubernetes context with fzf
        echo ""
        print_info "Select Kubernetes context (use arrow keys or type to filter):"
        NEW_CONTEXT=$(kubectl config get-contexts -o name | fzf --height=10 --reverse --header="Select Kubernetes context" --prompt="Context: ")
        
        if [[ -n "$NEW_CONTEXT" ]]; then
            kubectl config use-context "$NEW_CONTEXT"
            print_status "Switched to context: $NEW_CONTEXT"
        fi
        
        # Select namespace with fzf
        echo ""
        print_info "Select namespace (use arrow keys or type to filter):"
        NEW_NAMESPACE=$(kubectl get namespaces -o name | cut -d/ -f2 | fzf --height=10 --reverse --header="Select namespace" --prompt="Namespace: ")
        
        if [[ -n "$NEW_NAMESPACE" ]]; then
            kubectl config set-context --current --namespace="$NEW_NAMESPACE"
            print_status "Switched to namespace: $NEW_NAMESPACE"
        fi
    fi
done

# Select deployment with fzf
echo ""
print_info "Select deployment to intercept (use arrow keys or type to filter):"
DEPLOYMENT_NAME=$(kubectl get deployments -o name | cut -d/ -f2 | fzf --height=10 --reverse --header="Select deployment to intercept" --prompt="Deployment: ")

if [[ -z "$DEPLOYMENT_NAME" ]]; then
    print_error "No deployment selected. Exiting."
    exit 1
fi
print_status "Selected deployment: $DEPLOYMENT_NAME"

# Get containers in the deployment and let user select if multiple
echo ""
print_info "Getting containers for deployment $DEPLOYMENT_NAME..."
CONTAINERS=$(kubectl get deployment "$DEPLOYMENT_NAME" -o jsonpath='{.spec.template.spec.containers[*].name}')
CONTAINER_COUNT=$(echo "$CONTAINERS" | wc -w)

if [[ $CONTAINER_COUNT -eq 0 ]]; then
    print_error "No containers found in deployment $DEPLOYMENT_NAME"
    exit 1
elif [[ $CONTAINER_COUNT -eq 1 ]]; then
    CONTAINER_NAME="$CONTAINERS"
    print_status "Using container: $CONTAINER_NAME"
else
    print_info "Multiple containers found. Select container to intercept:"
    CONTAINER_NAME=$(echo "$CONTAINERS" | tr ' ' '\n' | fzf --height=10 --reverse --header="Select container to intercept" --prompt="Container: ")
    
    if [[ -z "$CONTAINER_NAME" ]]; then
        print_error "No container selected. Exiting."
        exit 1
    fi
    print_status "Selected container: $CONTAINER_NAME"
fi

# Compile the bootstrap runner for the intercepted container's architecture
echo ""
print_info "Detecting container architecture..."

# Get a pod from the deployment using the deployment's selectors
SELECTOR=$(kubectl get deployment "$DEPLOYMENT_NAME" -o jsonpath='{.spec.selector.matchLabels}' | jq -r 'to_entries | map("\(.key)=\(.value)") | join(",")')
POD_NAME=$(kubectl get pods -l "$SELECTOR" -o jsonpath='{.items[0].metadata.name}' 2>/dev/null)

if [[ -z "$POD_NAME" ]]; then
    print_warning "Could not find pod for deployment $DEPLOYMENT_NAME, using default amd64"
    CONTAINER_ARCH="amd64"
else
    # Get the node name from the pod
    NODE_NAME=$(kubectl get pod "$POD_NAME" -o jsonpath='{.spec.nodeName}' 2>/dev/null)
    
    if [[ -n "$NODE_NAME" ]]; then
        # Get the architecture from the node
        NODE_ARCH=$(kubectl get node "$NODE_NAME" -o jsonpath='{.status.nodeInfo.architecture}' 2>/dev/null || echo "amd64")
        
        # Map Kubernetes architecture names to Go architecture names
        case "$NODE_ARCH" in
            "amd64"|"x86_64")
                CONTAINER_ARCH="amd64"
                ;;
            "arm64"|"aarch64")
                CONTAINER_ARCH="arm64"
                ;;
            "arm")
                CONTAINER_ARCH="arm"
                ;;
            *)
                print_warning "Unknown architecture: $NODE_ARCH, defaulting to amd64"
                CONTAINER_ARCH="amd64"
                ;;
        esac
        
        print_status "Detected container architecture: $CONTAINER_ARCH"
    else
        print_warning "Could not get node information, defaulting to amd64"
        CONTAINER_ARCH="amd64"
    fi
fi

print_info "Compiling bootstrapRunner for linux/$CONTAINER_ARCH"
GOOS=linux GOARCH="$CONTAINER_ARCH" go build -C docker/kubernetes-agent-tentacle/bootstrapRunner -o "/tmp/k8s-agent-debug-vol/bootstrapRunner"
print_status "Bootstrap runner compiled successfully"


# Connect to cluster with Telepresence
echo ""
print_info "Connecting to cluster with Telepresence..."
telepresence connect

# Check if traffic manager is installed
echo ""
print_info "Checking for traffic manager..."
TRAFFIC_MANAGER_STATUS=$(telepresence status | grep "Traffic Manager" || echo "not found")

if [[ "$TRAFFIC_MANAGER_STATUS" == *"not found"* ]] || [[ "$TRAFFIC_MANAGER_STATUS" == *"not running"* ]]; then
    print_warning "Traffic Manager is not installed or not running"
    read -p "Would you like to install the Traffic Manager? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        print_info "Installing Traffic Manager..."
        telepresence helm install
        
        # Wait for traffic manager to be ready
        print_info "Waiting for Traffic Manager to be ready..."
        sleep 5
        kubectl wait --for=condition=available --timeout=60s deployment/traffic-manager -n ambassador
    else
        print_error "Traffic Manager is required for intercepts. Exiting."
        exit 1
    fi
fi
print_status "Traffic Manager is ready"

# No need to create mount directory - Telepresence will handle it

# Clean up function
cleanup() {
    echo ""
    print_info "Cleaning up..."
    
    # Remove the Telepresence profile from launchSettings.json
    if [[ -f "$LAUNCH_SETTINGS_PATH" ]]; then
        print_info "Removing Telepresence profile from launchSettings.json..."
        CLEANED_JSON=$(cat "$LAUNCH_SETTINGS_PATH" | jq --arg name "$PROFILE_NAME" 'del(.profiles[$name])')
        
        # Check if there are any profiles left
        PROFILE_COUNT=$(echo "$CLEANED_JSON" | jq '.profiles | length')
        if [[ "$PROFILE_COUNT" -eq 0 ]]; then
            # No profiles left, remove the file
            rm -f "$LAUNCH_SETTINGS_PATH"
            print_status "Removed empty launchSettings.json"
        else
            # Write the cleaned JSON back
            echo "$CLEANED_JSON" | jq '.' > "$LAUNCH_SETTINGS_PATH"
            print_status "Removed Telepresence profile from launchSettings.json"
        fi
    fi
    
    telepresence leave "$DEPLOYMENT_NAME" --container "$CONTAINER_NAME" 2>/dev/null || true
    print_status "Intercept removed"
}

# Set up trap for cleanup
trap cleanup EXIT INT TERM

# Create the intercept
echo ""
print_info "Creating replace for $DEPLOYMENT_NAME..."
print_info "Environment variables will be saved to: $ENV_FILE"
print_info "Volumes will be mounted via TELEPRESENCE_ROOT environment variable"

telepresence replace "$DEPLOYMENT_NAME" \
    --container "$CONTAINER_NAME" \
    --env-file "$ENV_FILE" \
    --mount=true

print_status "Intercept created successfully!"

# Get the TELEPRESENCE_ROOT from the env file
if [[ -f "$ENV_FILE" ]]; then
    TELEPRESENCE_ROOT=$(grep '^TELEPRESENCE_ROOT=' "$ENV_FILE" | cut -d'=' -f2)
    print_info "Telepresence mount root: $TELEPRESENCE_ROOT"
fi

# Create or update launchSettings.json from template
LAUNCH_SETTINGS_PATH="$PWD/source/Octopus.Tentacle/Properties/launchSettings.json"
TEMPLATE_PATH="$PWD/telepresence-config/launchSettings.template.json"

print_info "Creating launchSettings.json from template..."

# Ensure the Properties directory exists
mkdir -p "$(dirname "$LAUNCH_SETTINGS_PATH")"

# Read template and replace placeholders
launchSettings=$(cat "$TEMPLATE_PATH")
launchSettings="${launchSettings//<deployment-name>/$DEPLOYMENT_NAME}"
launchSettings="${launchSettings//<container-name>/$CONTAINER_NAME}"
launchSettings="${launchSettings//<namespace>/$CURRENT_NAMESPACE}"
launchSettings="${launchSettings//<telepresence-root>/${TELEPRESENCE_ROOT:-\$(TELEPRESENCE_ROOT)}}"

# Profile name for the Telepresence configuration
PROFILE_NAME="Telepresence - $DEPLOYMENT_NAME"

# Parse the environment file and build JSON for environment variables
print_info "Parsing environment variables from $ENV_FILE..."
ENV_VARS_JSON="{}"

# Extract existing environment variables from the template
TEMPLATE_ENV_VARS=$(echo "$launchSettings" | jq -r --arg name "$PROFILE_NAME" '.profiles[$name].environmentVariables // {} | keys[]' 2>/dev/null)

if [[ -f "$ENV_FILE" ]]; then
    # Define environment variables to skip (these might conflict or are handled elsewhere)
    SKIP_VARS=(
        "PATH"
        "HOME"
        "USER"
        "SHELL"
        "PWD"
        "OLDPWD"
        "SHLVL"
        "_"
        "TELEPRESENCE_ROOT"  # This is handled via workingDirectory
    )
    
    # Read the env file line by line
    while IFS= read -r line; do
        # Skip empty lines and comments
        [[ -z "$line" ]] && continue
        [[ "$line" =~ ^[[:space:]]*# ]] && continue
        
        # Parse key=value pairs
        if [[ "$line" =~ ^([^=]+)=(.*)$ ]]; then
            key="${BASH_REMATCH[1]}"
            value="${BASH_REMATCH[2]}"
            
            # Check if this key should be skipped (system variables)
            skip=false
            for skip_var in "${SKIP_VARS[@]}"; do
                if [[ "$key" == "$skip_var" ]]; then
                    skip=true
                    break
                fi
            done
            
            # Check if this key already exists in the template
            if [[ "$skip" == false ]] && echo "$TEMPLATE_ENV_VARS" | grep -q "^${key}$"; then
                print_warning "Skipping $key as it's already defined in template"
                skip=true
            fi
            
            if [[ "$skip" == false ]]; then
                # Just use the raw value directly - it's already properly formatted
                # Add to our JSON object using --arg to handle special characters
                ENV_VARS_JSON=$(echo "$ENV_VARS_JSON" | jq --arg key "$key" --arg value "$value" '.[$key] = $value')
            fi
        fi
    done < "$ENV_FILE"
    
    print_status "Parsed $(echo "$ENV_VARS_JSON" | jq 'keys | length') environment variables"
fi

# Check if launchSettings.json already exists
if [[ -f "$LAUNCH_SETTINGS_PATH" ]]; then
    print_info "Updating existing launchSettings.json..."
    
    # Read existing file or create empty JSON if invalid
    EXISTING_JSON=$(cat "$LAUNCH_SETTINGS_PATH" 2>/dev/null | jq '.' 2>/dev/null || echo '{"profiles":{}}')
    
    # Extract the new profile from the template
    NEW_PROFILE=$(echo "$launchSettings" | jq --arg name "$PROFILE_NAME" '.profiles[$name]')
    
    # Merge the parsed environment variables into the profile
    NEW_PROFILE=$(echo "$NEW_PROFILE" | jq --argjson envVars "$ENV_VARS_JSON" '
        .environmentVariables = (.environmentVariables // {}) + $envVars
    ')
    
    # Remove any existing Telepresence profiles and add the new one
    UPDATED_JSON=$(echo "$EXISTING_JSON" | jq --arg name "$PROFILE_NAME" --argjson profile "$NEW_PROFILE" '
        .profiles |= with_entries(select(.key | startswith("Telepresence") | not)) |
        .profiles[$name] = $profile
    ')
    
    # Write the updated JSON
    echo "$UPDATED_JSON" | jq '.' > "$LAUNCH_SETTINGS_PATH"
    print_status "Updated launchSettings.json with profile: $PROFILE_NAME"
else
    # File doesn't exist, create it
    print_info "Creating new launchSettings.json..."
    
    # Parse the template and merge environment variables
    MERGED_SETTINGS=$(echo "$launchSettings" | jq --arg name "$PROFILE_NAME" --argjson envVars "$ENV_VARS_JSON" '
        .profiles[$name].environmentVariables = (.profiles[$name].environmentVariables // {}) + $envVars
    ')
    
    echo "$MERGED_SETTINGS" | jq '.' > "$LAUNCH_SETTINGS_PATH"
    print_status "Created launchSettings.json with profile: $PROFILE_NAME"
fi

# Display information for the user
echo ""
echo "========================================="
echo "   Telepresence Intercept Active"
echo "========================================="
print_info "Deployment: $DEPLOYMENT_NAME"
print_info "Namespace: $CURRENT_NAMESPACE"
print_info "Environment variables: Automatically injected from pod"
print_info "Mount directory: Available via TELEPRESENCE_ROOT env var"
echo ""
print_info "To run the Tentacle locally:"
echo "  1. Open the project in your IDE"
echo "  2. Select the 'Telepresence - $DEPLOYMENT_NAME' launch profile"
echo "  3. Start debugging"
echo ""
print_info "The intercept will remain active. Press Ctrl+C to stop."
echo "========================================="

# Keep the script running
while true; do
    sleep 1
done