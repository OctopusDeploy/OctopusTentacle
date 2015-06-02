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

        public IBuiltInPackageRepository BuiltInRepository { get; set; }

        public INuGetFeed CreateRepository(string packageSource, ICredentials credentials)
        {
            Uri uri;
            if (Uri.TryCreate(packageSource, UriKind.RelativeOrAbsolute, out uri))
            {
                FeedCredentialsProvider.Instance.SetCredentials(uri, credentials);
            }

            return CreateRepository(packageSource);
        }

        public INuGetFeed CreateRepository(string packageSource)
        {
            if (packageSource == null)
                throw new ArgumentNullException("packageSource");

            if (BuiltInRepository != null && BuiltInRepository.IsBuiltInSource(packageSource))
                return BuiltInRepository.CreateRepository();

            var uri = new Uri(packageSource);
            return uri.IsFile
                ? new ExternalNuGetFeedAdapter(new FastLocalPackageRepository(uri.LocalPath))
                : new ExternalNuGetFeedAdapter(new DataServicePackageRepository(CreateHttpClient(uri)));
        }

        static IHttpClient CreateHttpClient(Uri uri)
        {
            var http = new RedirectedHttpClient(uri);
            http.SendingRequest += (sender, args) => CustomizeRequest(args.Request);
            return http;
        }

        static void CustomizeRequest(WebRequest request)
        {
            request.Timeout = 1000*60*100;
            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.ReadWriteTimeout = 1000*60*100;
            }
        }
    }
}