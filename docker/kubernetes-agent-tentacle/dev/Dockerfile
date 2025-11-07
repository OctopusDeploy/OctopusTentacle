ARG RuntimeDepsTag

FROM golang:1.24 AS bootstrapRunnerBuilder

ARG TARGETARCH
ARG TARGETOS

COPY docker/kubernetes-agent-tentacle/bootstrapRunner/* /bootstrapRunner/
WORKDIR /bootstrapRunner

# Note: the given ldflags remove debug symbols
RUN env GOOS=linux GOARCH=amd64 go build -ldflags "-s -w" -o "bin/bootstrapRunner/amd64"
RUN env GOOS=linux GOARCH=arm64 go build -ldflags "-s -w" -o "bin/bootstrapRunner/arm64"

FROM mcr.microsoft.com/dotnet/runtime-deps:$RuntimeDepsTag

ARG BUILD_NUMBER
ARG BUILD_DATE
ARG TARGETARCH
ARG TARGETOS
ARG TARGETVARIANT
ARG DEBUGGER_ARCH=linux-x64

EXPOSE 10933

# debugging port
EXPOSE 7777

COPY docker/kubernetes-agent-tentacle/scripts/* /scripts/
COPY --from=bootstrapRunnerBuilder bootstrapRunner/bin/bootstrapRunner /bootstrapRunner
RUN chmod +x /scripts/*.sh

COPY docker/kubernetes-agent-tentacle/dev/scripts/* /dev-scripts/
RUN chmod +x /dev-scripts/*.sh

WORKDIR /tmp

# Install Tentacle
COPY _artifacts/docker/tentacle_${BUILD_NUMBER}_${TARGETOS}-${TARGETARCH}${TARGETVARIANT}.deb /tmp/tentacle.deb
RUN apt-get update
RUN apt install ./tentacle.deb -y
RUN apt-get clean
RUN apt install curl p7zip-full -y
RUN rm -rf /var/lib/apt/lists/*

WORKDIR /

RUN \
    curl -L "https://download.jetbrains.com/rider/ssh-remote-debugging/${DEBUGGER_ARCH}/jetbrains_debugger_agent_20230319.24.0" \
    -o /usr/local/bin/debugger && \
    chmod +x /usr/local/bin/debugger 

# We know this won't reduce the image size at all. It's just to make the filesystem a little tidier.
RUN rm -rf /tmp/*

ENV BOOTSTRAPRUNNEREXECUTABLEPATH=/bootstrapRunner
ENV OCTOPUS_RUNNING_IN_CONTAINER=Y
ENV ACCEPT_EULA=N
ENV CustomPublicHostName=""
ENV ListeningPort=""
ENV MachinePolicy="Default Machine Policy"
ENV PublicHostNameConfiguration="ComputerName"
ENV ServerApiKey=""
ENV ServerPassword=""
ENV ServerUsername=""
ENV ServerCommsAddress=""
ENV ServerPort=""
ENV ServerUrl=""
ENV ServerSubscriptionId=""
ENV Space="Default"
ENV TargetEnvironment=""
ENV TargetName=""
ENV TargetRole=""
ENV TargetTenant=""
ENV TargetTenantTag=""
ENV TargetTenantedDeploymentParticipation=""
ENV OCTOPUS__K8STENTACLE__NAMESPACE=""
ENV OCTOPUS__K8STENTACLE__EXECUTEINLOCALSHELL="False"
ENV OCTOPUS__K8STENTACLE__PODSERVICEACCOUNTNAME=""
ENV OCTOPUS__K8STENTACLE__PODVOLUMEJSON=""
ENV OCTOPUS__K8STENTACLE__DISABLEAUTOPODCLEANUP="False"
ENV TentacleHome=""
ENV TentacleApplications=""
ENV TentacleCertificateBase64=""
ENV TentaclePollingProxyHost=""
ENV TentaclePollingProxyPort=""
ENV TentaclePollingProxyUsername=""
ENV TentaclePollingProxyPassword=""

ENTRYPOINT [ "/dev-scripts/bootstrap.sh" ] 

LABEL \
    org.label-schema.schema-version="1.0" \
    org.label-schema.name="Octopus Deploy Kubernetes Tentacle" \
    org.label-schema.vendor="Octopus Deploy" \
    org.label-schema.url="https://octopus.com" \
    org.label-schema.vcs-url="https://github.com/OctopusDeploy/OctopusTentacle" \
    org.label-schema.license="Apache"  \
    org.label-schema.description="Octopus Kubernetes Tentacle instance with auto-registration to Octopus Server" \
    org.label-schema.version=${BUILD_NUMBER} \
    org.label-schema.build-date=${BUILD_DATE}

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
