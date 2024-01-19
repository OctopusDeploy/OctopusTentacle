ARG IMAGE_TAG
FROM docker.packages.octopushq.com/octopusdeploy/kubernetes-tentacle:$IMAGE_TAG

RUN apt-get update && apt-get install -y curl p7zip-full

ARG DEBUGGER_ARCH=linux-x64

RUN \
    curl -L "https://download.jetbrains.com/rider/ssh-remote-debugging/${DEBUGGER_ARCH}/jetbrains_debugger_agent_20230319.24.0" \
    -o /usr/local/bin/debugger && \
    chmod +x /usr/local/bin/debugger

# This installs the required tools, but there are versioning issues and it isn't working as expected

#ARG DEBUGGER_TOOLS_ARCH=linux64
#ARG DEBUGGER_TOOLS_VERSION=2023.3.2

# RUN mkdir ~/.local && \
#     mkdir ~/.local/share && \
#     mkdir ~/.local/share/JetBrains && \
#     mkdir ~/.local/share/JetBrains/RiderRemoteDebugger && \
#     mkdir ~/.local/share/JetBrains/RiderRemoteDebugger/${DEBUGGER_TOOLS_VERSION}

# RUN \
#     curl -L "https://data.services.jetbrains.com/products/download?code=RRD&platform=${DEBUGGER_TOOLS_ARCH}" \
#     -o  ~/.local/share/JetBrains/RiderRemoteDebugger/${DEBUGGER_TOOLS_VERSION}/debug_tools.zip

# RUN \
#     cd ~/.local/share/JetBrains/RiderRemoteDebugger/${DEBUGGER_TOOLS_VERSION} \
#     7z x debug_tools.zip

# debugging port
EXPOSE 7777

COPY docker/kubernetes-tentacle/scripts/* /scripts/
RUN chmod +x /scripts/*.sh

COPY docker/kubernetes-tentacle/dev/scripts/* /dev-scripts/
RUN chmod +x /scripts/*.sh

CMD /dev-scripts/bootstrap.sh