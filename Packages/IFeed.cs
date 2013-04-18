using System;
using System.Net;

namespace Octopus.Shared.Packages
{
    public interface IFeed
    {
        string Id { get; }
        string FeedUri { get; }
        ICredentials GetCredentials();
    }
}