FROM docker.packages.octopushq.com/octopusdeploy/tool-containers/windowsservercore

ARG BUILD_NUMBER
ARG BUILD_DATE
ARG INSTALLATION_FOLDER="C:/Program Files/Octopus Deploy/Tentacle"

ENV TentacleVersion ${BUILD_NUMBER}
ENV OCTOPUS_RUNNING_IN_CONTAINER Y

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

EXPOSE 10933

# Either from a build artifact dependency or local build output.
COPY _artifacts/tentacle "${INSTALLATION_FOLDER}"

RUN setx path "%path%;C:\Program Files\Octopus Deploy\Tentacle"
RUN Tentacle create-instance --console --instance Tentacle --config C:\Octopus\Tentacle.config

WORKDIR /Scripts
COPY docker/windows/Scripts/*.ps1 /Scripts/

HEALTHCHECK --interval=20s --timeout=20s --retries=6 CMD powershell -file ./healthcheck-tentacle.ps1
ENTRYPOINT ["powershell.exe", "-File", "configure-and-run-tentacle-wrapper.ps1"]
