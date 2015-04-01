using System;
using System.Net;
using NuGet;

namespace Octopus.Shared.Packages
{
    public static class NuGetExtensions
    {
        public static IPackageRepository CreateRepository(this IPackageRepositoryFactory packageRepositoryFactory, string packageSource, ICredentials credentials)
        {
            Uri uri;
            if (Uri.TryCreate(packageSource, UriKind.RelativeOrAbsolute, out uri))
            {
                FeedCredentialsProvider.Instance.SetCredentials(uri, credentials);
            }

            return packageRepositoryFactory.CreateRepository(packageSource);
        }
    }
}