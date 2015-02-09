using System;
using System.Net;
using NuGet;
using Octopus.Shared.BuiltInFeed;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Packages
{
    public class OctopusPackageRepositoryFactory : IOctopusPackageRepositoryFactory
    {
        readonly ILog log;

        public OctopusPackageRepositoryFactory(ILog log)
        {
            this.log = log;
        }

        public IBuiltInPackageRepositoryFactory BuiltInRepositoryFactory { get; set; }

        public INuGetRepository CreateRepository(string packageSource, ICredentials credentials)
        {
            Uri uri;
            if (Uri.TryCreate(packageSource, UriKind.RelativeOrAbsolute, out uri))
            {
                FeedCredentialsProvider.Instance.SetCredentials(uri, credentials);
            }

            return CreateRepository(packageSource);
        }

        public INuGetRepository CreateRepository(string packageSource)
        {
            if (packageSource == null)
                throw new ArgumentNullException("packageSource");

            if (BuiltInRepositoryFactory != null && BuiltInRepositoryFactory.IsBuiltInSource(packageSource))
                return BuiltInRepositoryFactory.CreateRepository();

            var uri = new Uri(packageSource);
            return uri.IsFile 
                ? new ExternalNuGetRepositoryAdapter(new FastLocalPackageRepository(uri.LocalPath, log)) 
                : new ExternalNuGetRepositoryAdapter(new DataServicePackageRepository(CreateHttpClient(uri)));
        }

        static IHttpClient CreateHttpClient(Uri uri)
        {
            var http = new RedirectedHttpClient(uri);
            http.SendingRequest += (sender, args) => CustomizeRequest(args.Request);
            return http;
        }

        static void CustomizeRequest(WebRequest request)
        {
            request.Timeout = 1000 * 60 * 100;
            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.ReadWriteTimeout = 1000 * 60 * 100;
            }
        }
    }
}