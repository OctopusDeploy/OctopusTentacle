using System;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;

public interface IToolDownloader
{
    Task<string> Download(string targetDirectory, CancellationToken cancellationToken);
}