FROM ubuntu:24.04
ENV ASPNETCORE_URLS=http://+:80 DOTNET_RUNNING_IN_CONTAINER=true

# .NET 8 runtime dependencies.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        libicu74 \
        libssl3t64 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ARG BUILD_NUMBER
ARG BUILD_DATE

# The CLI tools this image is expected to provide; curl, jq, lsb-release, sudo, xxd and openssl are expected in the published surface.
# Customer deployment steps and Kubernetes lifecycle hooks shell out to them, so don't drop one just because nothing in this repo calls it.
# We use --no-install-recommends to avoid unexpectedly taking on new implied dependencies.
# See: https://github.com/OctopusDeploy/OctopusTentacle/pull/1312.
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
        dos2unix \
        jq \
        lsb-release \
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

# The ubuntu:24.04 base image ships an EMPTY /etc/machine-id. It is populated by the dbus and systemd packages that
# install-docker.sh pulls in as dependencies: their postinst scripts generate a fresh id on every image build, so
# every container started from one tag shares it and every tag has a different one. That is why this step must stay
# after the Docker install. Tentacle no longer derives its configuration encryption key from the machine-id, but
# leaving it empty makes sure nothing can, and is what systemd's machine-id(5) requires of an image that runs on
# many machines. Both files have to go: dbus-uuidgen and systemd-machine-id-setup each copy the other's if only one
# is present. Nothing regenerates it at start-up on purpose: a fresh id on every start would be useless as a key.
RUN truncate -s 0 /etc/machine-id && rm -f /var/lib/dbus/machine-id

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
