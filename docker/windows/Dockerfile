FROM docker.packages.octopushq.com/octopusdeploy/tool-containers/windowsservercore

ARG BUILD_NUMBER
ARG BUILD_DATE
ARG INSTALLATION_FOLDER="C:/Program Files/Octopus Deploy/Tentacle"

ENV TentacleVersion ${BUILD_NUMBER}
ENV OCTOPUS_RUNNING_IN_CONTAINER Y

LABEL \
    org.opencontainers.image.title="Octopus Deploy Tentacle" \
    org.opencontainers.image.vendor="Octopus Deploy" \
    org.opencontainers.image.url="https://octopus.com" \
    org.opencontainers.image.source="https://github.com/OctopusDeploy/OctopusTentacle" \
    org.opencontainers.image.licenses="Apache-2.0" \
    org.opencontainers.image.description="Octopus Tentacle instance with auto-registration to Octopus Server" \
    org.opencontainers.image.version=${BUILD_NUMBER} \
    org.opencontainers.image.created=${BUILD_DATE}

EXPOSE 10933

# Either from a build artifact dependency or local build output.
COPY _artifacts/tentacle "${INSTALLATION_FOLDER}"

RUN setx path "%path%;C:\Program Files\Octopus Deploy\Tentacle"
RUN Tentacle create-instance --console --instance Tentacle --config C:\Octopus\Tentacle.config

WORKDIR /Scripts
COPY docker/windows/Scripts/*.ps1 /Scripts/

HEALTHCHECK --interval=20s --timeout=20s --retries=6 CMD powershell -file ./healthcheck-tentacle.ps1
ENTRYPOINT ["powershell.exe", "-File", "configure-and-run-tentacle-wrapper.ps1"]
