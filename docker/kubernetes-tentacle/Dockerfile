FROM mcr.microsoft.com/dotnet/runtime-deps:6.0

ARG BUILD_NUMBER
ARG BUILD_DATE

EXPOSE 10933

WORKDIR /tmp

COPY docker/kubernetes-tentacle/scripts/* /scripts/
RUN chmod +x /scripts/*.sh

# Install Tentacle
COPY _artifacts/deb/tentacle_${BUILD_NUMBER}_amd64.deb /tmp/
RUN apt-get update && \
    apt install ./tentacle_${BUILD_NUMBER}_amd64.deb && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

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
ENV OCTOPUS__TENTACLE__K8SNAMESPACE="octopus-tentacle"
ENV OCTOPUS__TENTACLE__K8SUSEJOBS="True"
ENV OCTOPUS__TENTACLE__K8SSERVICEACCOUNTNAME="octopus-tentacle-job"
ENV OCTOPUS__TENTACLE__K8SJOBVOLUMEYAML=""

CMD /scripts/configure-tentacle.sh && /scripts/run-tentacle.sh

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
