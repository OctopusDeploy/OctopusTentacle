using System;
using System.Net;

namespace Octopus.Shared.Util
{
    public static class FeedPing
    {
        public static void Ping(string feedUri)
        {
            if (feedUri.StartsWith("http://") || feedUri.StartsWith("https://"))
            {
                feedUri = feedUri.TrimEnd('/');
                if (!feedUri.EndsWith("/Packages"))
                {
                    feedUri += "/Packages";
                }

                var client = new WebClient();
                client.DownloadString(feedUri);
            }
        }
    }
}
