using System;
using System.Net;
using Octopus.Platform.Security.MasterKey;

namespace Octopus.Platform.Packages
{
    public interface IFeed
    {
        string Id { get; }
        string FeedUri { get; }
        ICredentials GetCredentials(IMasterKeyEncryption encryption);
    }
}