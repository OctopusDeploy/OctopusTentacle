using System;

namespace Octopus.Tentacle.Util
{
    public class PollingSubscriptionId
    {
        public static Uri Generate()
        {
            return new Uri("poll://" + RandomStringGenerator.Generate(20).ToLowerInvariant() + "/");
        }
    }
}