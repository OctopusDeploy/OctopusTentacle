FROM mcr.microsoft.com/dotnet/runtime-deps:6.0

ARG BUILD_NUMBER
ARG BUILD_DATE
ARG TARGETARCH
ARG TARGETOS
ARG TARGETVARIANT

EXPOSE 10933

COPY docker/kubernetes-tentacle/scripts/* /scripts/
RUN chmod +x /scripts/*.sh

WORKDIR /tmp

# Install Tentacle
COPY _artifacts/docker/tentacle_${BUILD_NUMBER}_${TARGETOS}-${TARGETARCH}${TARGETVARIANT}.deb /tmp/tentacle.deb
RUN apt-get update
RUN apt install ./tentacle.deb -y
RUN apt-get clean
RUN apt install curl tini -y
RUN rm -rf /var/lib/apt/lists/*

WORKDIR /

# We know this won't reduce the image size at all. It's just to make the filesystem a little tidier.
RUN rm -rf /tmp/*

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

# We use tini here as the entrypoint so it correctly propagates SIGTERM signals to the tentacle child process (allowing for graceful shutdown to occur)
ENTRYPOINT ["/usr/bin/tini", "-g", "--", "/scripts/configure-and-run.sh"]

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
