using System;
using System.Net;
using NuGet;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Packages
{
    public class OctopusPackageRepositoryFactory : IPackageRepositoryFactory
    {
        readonly ILog log;
        
        public OctopusPackageRepositoryFactory(ILog log)
        {
            this.log = log;
        }

        public IPackageRepository CreateRepository(string packageSource)
        {
            if (packageSource == null)
                throw new ArgumentNullException("packageSource");

            var uri = new Uri(packageSource);
            if (uri.IsFile)
                return new FastLocalPackageRepository(uri.LocalPath, log);

            return new DataServicePackageRepository(CreateHttpClient(uri));
        }

        RedirectedHttpClient CreateHttpClient(Uri uri)
        {
            var http = new RedirectedHttpClient(uri);
            http.SendingRequest += (sender, args) => CustomizeRequest(args.Request);
            return http;
        }

        void CustomizeRequest(WebRequest request)
        {
            request.Timeout = 1000 * 60 * 10;
            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.ReadWriteTimeout = 1000 * 60 * 10;
            }
        }
    }
}