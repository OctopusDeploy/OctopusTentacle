using System;
using System.Net;
using NuGet;
using log4net;

namespace Octopus.Shared.Packages
{
    public class OctopusPackageRepositoryFactory : IPackageRepositoryFactory
    {
        readonly ILog log;
        readonly Func<Uri, IHttpClient> httpClientFactory;

        public OctopusPackageRepositoryFactory(ILog log)
        {
            this.log = log;
            
            httpClientFactory = uri =>
            {
                var http = new RedirectedHttpClient(uri);
                http.SendingRequest += (sender, args) =>
                {
                    args.Request.Timeout = 1000*60*10;
                    var httpWebRequest = args.Request as HttpWebRequest;
                    if (httpWebRequest != null)
                    {
                        httpWebRequest.ReadWriteTimeout = 1000*60*10;
                    }
                };
                return http;
            };
        }

        public IPackageRepository CreateRepository(string packageSource)
        {
            if (packageSource == null)
                throw new ArgumentNullException("packageSource");

            var uri = new Uri(packageSource);
            if (uri.IsFile)
                return new FastLocalPackageRepository(uri.LocalPath, log);
            
            return new DataServicePackageRepository(httpClientFactory(uri));
        }
    }
}