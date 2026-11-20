# Build context: repo root. docker build -f source/Octopus.Pipelines.Runner.Kubernetes.Host/Dockerfile .
# The project folders copied below must match the allow-list in Dockerfile.dockerignore; add a project to both or the build can't see it.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props NuGet.Config ./
COPY source/Octopus.Pipelines.Runner.props source/
COPY source/Octopus.Pipelines.Runner.Contracts/Octopus.Pipelines.Runner.Contracts.csproj source/Octopus.Pipelines.Runner.Contracts/
COPY source/Octopus.Pipelines.Runner.Logs.AzureBlob/Octopus.Pipelines.Runner.Logs.AzureBlob.csproj source/Octopus.Pipelines.Runner.Logs.AzureBlob/
COPY source/Octopus.Pipelines.Runner.Core/Octopus.Pipelines.Runner.Core.csproj source/Octopus.Pipelines.Runner.Core/
COPY source/Octopus.Pipelines.Runner.Gateway/Octopus.Pipelines.Runner.Gateway.csproj source/Octopus.Pipelines.Runner.Gateway/
COPY source/Octopus.Pipelines.Runner.Kubernetes/Octopus.Pipelines.Runner.Kubernetes.csproj source/Octopus.Pipelines.Runner.Kubernetes/
COPY source/Octopus.Pipelines.Runner.Kubernetes.Host/Octopus.Pipelines.Runner.Kubernetes.Host.csproj source/Octopus.Pipelines.Runner.Kubernetes.Host/
RUN dotnet restore source/Octopus.Pipelines.Runner.Kubernetes.Host/Octopus.Pipelines.Runner.Kubernetes.Host.csproj
COPY source/Octopus.Pipelines.Runner.Contracts/ source/Octopus.Pipelines.Runner.Contracts/
COPY source/Octopus.Pipelines.Runner.Logs.AzureBlob/ source/Octopus.Pipelines.Runner.Logs.AzureBlob/
COPY source/Octopus.Pipelines.Runner.Core/ source/Octopus.Pipelines.Runner.Core/
COPY source/Octopus.Pipelines.Runner.Gateway/ source/Octopus.Pipelines.Runner.Gateway/
COPY source/Octopus.Pipelines.Runner.Kubernetes/ source/Octopus.Pipelines.Runner.Kubernetes/
COPY source/Octopus.Pipelines.Runner.Kubernetes.Host/ source/Octopus.Pipelines.Runner.Kubernetes.Host/
RUN dotnet publish source/Octopus.Pipelines.Runner.Kubernetes.Host/Octopus.Pipelines.Runner.Kubernetes.Host.csproj -c Release --no-restore -o /app

# The runner runs jobs in pods, not locally, so it doesn't need a shell.
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Octopus.Pipelines.Runner.Kubernetes.Host.dll"]
