using System;
using System.Net;

namespace Octopus.Shared.Packages
{
    public interface IOctopusPackageRepositoryFactory
    {
        INuGetRepository CreateRepository(string packageSource);
        INuGetRepository CreateRepository(string packageSource, ICredentials credentials);
    }
}