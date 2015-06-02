using System;
using System.Net;

namespace Octopus.Shared.Packages
{
    public class NuGetFeedProperties : IFeed
    {
        public string Id { get; set; }
        public string FeedUri { get; set; }
        public string FeedUsername { get; set; }
        public string FeedPassword { get; set; }

        public ICredentials GetCredentials()
        {
            return string.IsNullOrWhiteSpace(FeedUsername)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(FeedUsername, FeedPassword);
        }
    }
}