FROM ubuntu:22.04
ENV ASPNETCORE_URLS=http://+:80 DOTNET_RUNNING_IN_CONTAINER=true

# .NET 8 runtime dependencies.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        libicu70 \
        libssl3 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ARG BUILD_NUMBER
ARG BUILD_DATE

# CLI tools this image is expected to provide. curl, jq, sudo, xxd and openssl are published
# surface - customer deployment steps and Kubernetes lifecycle hooks shell out to them - so
# don't drop one just because nothing in this repo calls it. install-docker.sh reinstalls
# every one of these except xxd; they stay declared here because that list is Docker's to
# change, not ours, and dropping them from here saves no image size.
#   curl     - public-IP lookup in configure-tentacle.sh; customer postStart/preStop hooks.
#   dos2unix - install-docker.sh strips CRLF from dind and dockerd-entrypoint.sh (build only).
#   jq       - customer hooks and scripts parsing Octopus API responses (#278).
#   sudo     - deployment scripts that escalate; also a dind runtime dep for --userns-remap.
#   xxd      - was Calamari's bash variable-set decoder (#1157); need removed by Calamari#1797.
#   openssl  - Calamari's bash bootstrap pipes every service message and variable through it,
#              so declare it rather than inheriting it from install-docker.sh (LEV-1843).
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        dos2unix \
        jq \
        sudo \
        xxd \
        openssl && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 10933

WORKDIR /tmp

COPY docker/linux/install-scripts/* /install-scripts/
RUN chmod +x /install-scripts/*.sh

COPY docker/linux/scripts/* /scripts/
RUN chmod +x /scripts/*.sh

# Install Docker daemon and CLI
COPY docker/linux/scripts/dockerd-entrypoint.sh /usr/local/bin/
RUN /install-scripts/install-docker.sh

# Install Tentacle
COPY _artifacts/deb/tentacle_${BUILD_NUMBER}_amd64.deb /tmp/
RUN apt-get update && \
    apt-get install -y --no-install-recommends ./tentacle_${BUILD_NUMBER}_amd64.deb && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    ln -s /opt/octopus/tentacle/Tentacle /usr/bin/tentacle
    
WORKDIR /

# We know this won't reduce the image size at all. It's just to make the filesystem a little tidier.
RUN rm -rf /tmp/*

ENV OCTOPUS_RUNNING_IN_CONTAINER=Y
ENV ACCEPT_EULA=N
ENV AsKubernetesTentacle=""
ENV BearerToken=""
ENV CustomPublicHostName=""
ENV DISABLE_DIND=N
ENV ListeningPort=""
ENV MachinePolicy="Default Machine Policy"
ENV PollingConnectionCount=""
ENV PublicHostNameConfiguration="ComputerName"
ENV ProxyName=""
ENV ServerApiKey=""
ENV ServerPassword=""
ENV ServerCommsAddress=""
ENV ServerPort=""
ENV ServerUrl=""
ENV ServerUsername=""
ENV Space="Default"
ENV TargetEnvironment=""
ENV TargetName=""
ENV TargetRole=""
ENV TargetTenant=""
ENV TargetTenantTag=""
ENV TargetTenantedDeploymentParticipation=""
ENV TargetWorkerPool=""

VOLUME /var/lib/docker

ENTRYPOINT [ "/scripts/configure-and-run.sh" ]

LABEL \
    org.label-schema.schema-version="1.0" \
    org.label-schema.name="Octopus Deploy Tentacle" \
    org.label-schema.vendor="Octopus Deploy" \
    org.label-schema.url="https://octopus.com" \
    org.label-schema.vcs-url="https://github.com/OctopusDeploy/OctopusTentacle" \
    org.label-schema.license="Apache"  \
    org.label-schema.description="Octopus Tentacle instance with auto-registration to Octopus Server" \
    org.label-schema.version=${BUILD_NUMBER} \
    org.label-schema.build-date=${BUILD_DATE}
