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

ENV ServerUrl="https://octopus.example.com/"
ENV ServerPort=10943
ENV ServerApiKey="API-SOURCE-THIS-FROM-YOUR-OCTOPUS-SERVER"
ENV Space="Default"
ENV WorkerPool="Default Worker Pool"
ENV CommunicationsStype=TentacleActive
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
