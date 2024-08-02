FROM golang:1.22 as bootstrapRunnerBuilder

ARG TARGETARCH
ARG TARGETOS

COPY docker/kubernetes-agent-tentacle/bootstrapRunner/* /bootstrapRunner/
WORKDIR /bootstrapRunner

# Note: the given ldflags remove debug symbols
RUN go build -ldflags "-s -w" -o "bin/bootstrapRunner"

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0

ARG BUILD_NUMBER
ARG BUILD_DATE
ARG TARGETARCH
ARG TARGETOS
ARG TARGETVARIANT

EXPOSE 10933

COPY docker/kubernetes-agent-tentacle/scripts/* /scripts/
COPY --from=bootstrapRunnerBuilder bootstrapRunner/bin/bootstrapRunner /bootstrapRunner
RUN chmod +x /scripts/*.sh

WORKDIR /tmp

# Install Tentacle
COPY _artifacts/docker/tentacle_${BUILD_NUMBER}_${TARGETOS}-${TARGETARCH}${TARGETVARIANT}.deb /tmp/tentacle.deb
RUN apt-get update  \
    && apt install ./tentacle.deb curl -y  \
    && apt-get clean  \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /

# We know this won't reduce the image size at all. It's just to make the filesystem a little tidier.
RUN rm -rf /tmp/*

# Make the /.dotnet directory so that we can ensure ownership for the
# certificate store 
RUN mkdir /.dotnet

# By default, OpenShift uses random UIDs 
# This introduces support for it as per
# https://docs.openshift.com/container-platform/4.15/openshift_images/create-images.html#use-uid_create-images
RUN chgrp -R 0 /opt /usr /.dotnet && \
    chmod -R g=u /opt /usr /.dotnet

# The same as above, but we shouldn't recursively own /etc, as it would add
# ownership to system files like /etc/passwd
# We own /etc/ssl/certs so we can do a rehash
RUN chgrp 0 /etc /etc/ssl/certs && \
    chmod g=u /etc /etc/ssl/certs

ENV BOOTSTRAPRUNNEREXECUTABLEPATH=/bootstrapRunner
ENV OCTOPUS_RUNNING_IN_CONTAINER=Y
ENV ACCEPT_EULA=N
ENV CustomPublicHostName=""
ENV ListeningPort=""
ENV MachinePolicy="Default Machine Policy"
ENV PublicHostNameConfiguration="ComputerName"
ENV BearerToken=""
ENV ServerApiKey=""
ENV ServerPassword=""
ENV ServerUsername=""
ENV ServerCommsAddress=""
ENV ServerPort=""
ENV ServerUrl=""
ENV ServerSubscriptionId=""
ENV Space="Default"
ENV DeploymentTargetEnabled="False"
ENV WorkerEnabled="False"
ENV TargetEnvironment=""
ENV AgentName=""
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

ENTRYPOINT ["/scripts/configure-and-run.sh"]

LABEL \
    org.label-schema.schema-version="1.0" \
    org.label-schema.name="Octopus Deploy Kubernetes Agent Tentacle" \
    org.label-schema.vendor="Octopus Deploy" \
    org.label-schema.url="https://octopus.com" \
    org.label-schema.vcs-url="https://github.com/OctopusDeploy/OctopusTentacle" \
    org.label-schema.license="Apache"  \
    org.label-schema.description="Octopus Kubernetes Agent Tentacle instance with auto-registration to Octopus Server" \
    org.label-schema.version=${BUILD_NUMBER} \
    org.label-schema.build-date=${BUILD_DATE}
