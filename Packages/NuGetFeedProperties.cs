using System;
using System.Net;
using Octopus.Platform.Packages;
using Octopus.Platform.Security.MasterKey;

namespace Octopus.Platform.Deployment.Packages
{
    public class NuGetFeedProperties : IFeed
    {
        public string Id { get; set; }
        public string FeedUri { get; set; }
        public string FeedUsername { get; set; }
        public string FeedPassword { get; set; }

        public ICredentials GetCredentials(IMasterKeyEncryption encryption)
        {
            return string.IsNullOrWhiteSpace(FeedUsername) 
                ? CredentialCache.DefaultNetworkCredentials 
                : new NetworkCredential(FeedUsername, FeedPassword);
        }
    }
}