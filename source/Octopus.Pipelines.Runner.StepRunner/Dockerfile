# The step runner image: nothing but the statically linked step runner. A step pod's init container runs
# `/step-runner install <dir>` to copy it into an emptyDir that the step's own container runs it from.
# Build from the publish directory:
#   dotnet publish -c Release -r linux-<arch> -o <dir>
#   docker build -f Dockerfile <dir>
FROM scratch
COPY octopus-step-runner /step-runner
USER 65534:65534
ENTRYPOINT ["/step-runner"]
