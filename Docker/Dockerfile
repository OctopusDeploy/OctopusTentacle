ARG SERVERCORE_VERSION
ARG BASE_IMAGE
FROM ${BASE_IMAGE}:${SERVERCORE_VERSION}

HEALTHCHECK --interval=20s --timeout=20s --retries=6 CMD powershell -file ./Tentacle/healthcheck-tentacle.ps1

EXPOSE 10933

ADD Docker/Scripts/*.ps1 /Scripts/

ARG TentacleVersion
ENV TentacleVersion ${TentacleVersion}

LABEL \
    org.label-schema.schema-version="1.0" \
    org.label-schema.name="Octopus Deploy Tentacle" \
    org.label-schema.vendor="Octopus Deploy" \
    org.label-schema.url="https://octopus.com" \
    org.label-schema.vcs-url="https://github.com/OctopusDeploy/OctopusTentacle" \
    org.label-schema.license="Apache"  \
    org.label-schema.description="Octopus Tentacle instance with auto-registration to Octopus Server" \
    org.label-schema.build-date=$BUILD_DATE

WORKDIR /Scripts
RUN powershell ./build.ps1 -Verbose

ENTRYPOINT powershell -File ./configure-tentacle.ps1 -Verbose  && powershell -File ./run-tentacle.ps1 -Verbose
