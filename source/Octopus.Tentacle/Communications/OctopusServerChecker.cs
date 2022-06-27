using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Communications
{
    public class OctopusServerChecker : IOctopusServerChecker
    {
        readonly ISystemLog log;

        public OctopusServerChecker(ISystemLog log)
        {
            this.log = log;
        }

        public string CheckServerCommunicationsIsOpen(Uri serverAddress, IWebProxy proxyOverride)
        {
            Uri handshake;
            if (ServiceEndPoint.IsWebSocketAddress(serverAddress))
            {
                handshake = new UriBuilder(serverAddress) { Scheme = Uri.UriSchemeHttps }.Uri;
                log.Info($"Checking connectivity on the server web socket address {serverAddress}...");
            }
            else
            {
                handshake = new UriBuilder(serverAddress) { Path = "/handshake" }.Uri;
                log.Info($"Checking connectivity on the server communications port {serverAddress.Port}...");
            }
            string thumbprint = null;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                thumbprint = (certificate as X509Certificate2)?.Thumbprint;
                return true;
            };

            Retry("Checking that server communications are open", () =>
            {
#pragma warning disable DE0003
                var req = WebRequest.Create(handshake);
                req.Proxy = proxyOverride;
                req.Method = "POST";
                req.ContentLength = 0;
#pragma warning restore DE0003
                try
                {
                    using (var resp = req.GetResponse())
                    using (var rs = resp.GetResponseStream())
                    using (var reader = new StreamReader(rs))
                    {
                        var wr = (HttpWebResponse)resp;
                        var content = reader.ReadToEnd();
                        if (wr.StatusCode != HttpStatusCode.OK)
                            throw new Exception("The service listening on " + serverAddress + " does not appear to be an Octopus Server. The response code was: " + wr.StatusCode + ". The response was: " + content);

                        log.Verbose("Connectivity with the server communications port successfully verified.");
                    }
                }
                catch (WebException wex)
                {
                    var wr = (HttpWebResponse)wex.Response;

                    // "The remote server returned an error: (400) Bad Request."
                    // Server requires we send a cert, which we didn't. Port must be open.
                    if (wr == null || wr.StatusCode != HttpStatusCode.BadRequest)
                        throw;

                    log.Verbose("Connectivity with the server communications port successfully verified.");
                }
            }, 5, TimeSpan.FromSeconds(0.5));

            log.Info("Connected successfully");

            return thumbprint;
        }

         void Retry(string actionDescription, Action action, int retryCount, TimeSpan initialDelay, double backOffFactor = 1.5)
        {
            var delay = initialDelay;
            for (var i = 1; i <= retryCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (Exception ex)
                {
                    if (i >= retryCount)
                    {
                        log.ErrorFormat(ex, "{0} failed with message {1} after {2} retries.", actionDescription, ex.Message, i);
                        throw;
                    }

                    delay = new TimeSpan((long)(delay.Ticks * backOffFactor));
                    log.WarnFormat(ex, "{0} failed with message {1}. Retrying ({2}/{3}) in {4}.", actionDescription, ex.Message, i, retryCount, delay);
                }
            }
        }
    }
}