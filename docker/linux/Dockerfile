FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1

ARG BUILD_NUMBER
ARG BUILD_DATE

RUN apt-get update && \
    apt-get install -y curl sudo dos2unix && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

EXPOSE 10933

WORKDIR /tmp

COPY docker/linux/install-scripts/* /install-scripts/
COPY docker/linux/scripts/* /scripts/

# Install Docker daemon and CLI
COPY docker/linux/scripts/dockerd-entrypoint.sh /usr/local/bin/
RUN /install-scripts/install-docker.sh

# Install Tentacle
COPY tentacle_${BUILD_NUMBER}_amd64.deb /tmp/
RUN apt-get update && \
    apt install ./tentacle_${BUILD_NUMBER}_amd64.deb && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /

# We know this won't reduce the image size at all. It's just to make the filesystem a little tidier.
RUN rm -rf /tmp/*

ENV OCTOPUS_TENTACLE_DISPLAY_NAME="Octopus Tentacle"
ENV OCTOPUS_TENTACLE_CONFIG_DIR=/etc/octopus
ENV OCTOPUS_TENTACLE_APPLICATIONS_DIR=/home/Octopus/Applications
ENV OCTOPUS_TENTACLE_COMMS_STYLE=TentacleActive
ENV OCTOPUS_SERVER_URL="https://octopus.example.com/"
ENV OCTOPUS_SERVER_PORT=10943
ENV OCTOPUS_SERVER_API_KEY="API-SOURCE-THIS-FROM-YOUR-OCTOPUS-SERVER"
ENV OCTOPUS_SERVER_SPACE="Default"
ENV OCTOPUS_SERVER_WORKER_POOL="Default Worker Pool"
ENV ENABLE_DIND=Y
ENV ACCEPT_EULA=N

CMD /scripts/configure-tentacle.sh && /scripts/run-tentacle.sh

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
