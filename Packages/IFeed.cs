using System;
using System.Net;
using Octopus.Shared.Security.MasterKey;

namespace Octopus.Shared.Packages
{
    public interface IFeed
    {
        string Id { get; }
        string FeedUri { get; }
        ICredentials GetCredentials(IMasterKeyEncryption encryption);
    }
}