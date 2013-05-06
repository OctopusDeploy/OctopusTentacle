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
        
        public OctopusPackageRepositoryFactory(ILog log, IOctopusConfiguration configuration)
        {
            this.log = log;
            this.configuration = configuration;
        }

        public IPackageRepository CreateRepository(string packageSource)
        {
            if (packageSource == null)
                throw new ArgumentNullException("packageSource");

            bool isIntegrated = packageSource == "{IntegratedNuGetServer}";
            if (isIntegrated)
            {
                packageSource = new Uri(new Uri(configuration.LocalWebPortalAddress), "/api/odata").ToString();
                log.Debug("Using integrated NuGet feed: " + packageSource);
            }

            var uri = new Uri(packageSource);
            if (uri.IsFile)
                return new FastLocalPackageRepository(uri.LocalPath, log);

            var downloader = new NuGet.PackageDownloader();
            downloader.SendingRequest += ((sender, args) => CustomizeRequest(args.Request, isIntegrated));
            return new DataServicePackageRepository(CreateHttpClient(uri, isIntegrated), downloader);
        }

        RedirectedHttpClient CreateHttpClient(Uri uri, bool addApiKeyHeader)
        {
            var http = new RedirectedHttpClient(uri);
            http.SendingRequest += (sender, args) => CustomizeRequest(args.Request, addApiKeyHeader);
            return http;
        }

        void CustomizeRequest(WebRequest request, bool addApiKeyHeader)
        {
            if (addApiKeyHeader)
            {
                request.Headers.Add("X-Octopus-NuGetApiKey", configuration.IntegratedFeedApiKey);
            }

            request.Timeout = 1000 * 60 * 10;
            var httpWebRequest = request as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.ReadWriteTimeout = 1000 * 60 * 10;
            }
        }
    }
}