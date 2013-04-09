using System;
using System.Net;
using System.Runtime.Serialization;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Contracts
{
    [DataContract(Namespace = "http://schemas.octopusdeploy.com/deployment/v1")]
    public class NuGetFeedProperties : IFeed
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string FeedUri { get; set; }

        [DataMember]
        public string FeedUsername { get; set; }

        [DataMember]
        public string FeedPassword { get; set; }

        public ICredentials GetCredentials()
        {
            return string.IsNullOrWhiteSpace(FeedUsername) 
                ? CredentialCache.DefaultNetworkCredentials 
                : new NetworkCredential(FeedUsername, FeedPassword);
        }
    }
}