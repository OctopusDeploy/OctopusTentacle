using System;
using System.Net;
using EnterpriseDT.Google.GData.Client;
using NuGet;
using Octopus.Shared.Configuration;
using log4net;

namespace Octopus.Shared.Packages
{
    public class OctopusPackageRepositoryFactory : IPackageRepositoryFactory
    {
        readonly ILog log;
        readonly IOctopusConfiguration configuration;
        readonly Func<Uri, IHttpClient> httpClientFactory;

        public OctopusPackageRepositoryFactory(ILog log, IOctopusConfiguration configuration)
        {
            this.log = log;
            this.configuration = configuration;

            httpClientFactory = uri =>
            {
                var http = new RedirectedHttpClient(uri);
                http.SendingRequest += (sender, args) =>
                {
                    args.Request.Headers.Add("X-Octopus-NuGetApiKey", configuration.IntegratedFeedApiKey);
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

            if (packageSource == "{IntegratedNuGetServer}")
            {
                packageSource = new Uri(new Uri(configuration.LocalWebPortalAddress), "/api/odata").ToString();
                log.Debug("Using integrated NuGet feed: " + packageSource);
            }

            var uri = new Uri(packageSource);
            if (uri.IsFile)
                return new FastLocalPackageRepository(uri.LocalPath, log);
            
            return new DataServicePackageRepository(httpClientFactory(uri));
        }
    }
}