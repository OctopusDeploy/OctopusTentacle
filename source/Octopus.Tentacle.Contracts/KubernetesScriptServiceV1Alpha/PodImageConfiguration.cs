using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public class PodImageConfiguration
    {
        [JsonConstructor]
        public PodImageConfiguration(string image, string? feedUrl, string? feedUsername, string? feedPassword)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public PodImageConfiguration()
        {
        }


        public string? Image { get; }

        public string? FeedUrl { get; }

        public string? FeedUsername { get; }

        public string? FeedPassword { get; }
    }
}