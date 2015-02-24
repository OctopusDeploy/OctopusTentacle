using System;
using System.Net;

namespace Octopus.Shared.Packages
{
    public interface IOctopusPackageRepositoryFactory
    {
        INuGetFeed CreateRepository(string packageSource);
        INuGetFeed CreateRepository(string packageSource, ICredentials credentials);
    }
}