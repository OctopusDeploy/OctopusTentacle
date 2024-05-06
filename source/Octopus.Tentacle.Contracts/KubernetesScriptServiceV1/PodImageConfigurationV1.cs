using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1
{
    public class PodImageConfigurationV1
    {
        [JsonConstructor]
        public PodImageConfigurationV1(string image, string? feedUrl, string? feedUsername, string? feedPassword)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public PodImageConfigurationV1()
        {
        }


        public string? Image { get; }

        public string? FeedUrl { get; }

        public string? FeedUsername { get; }

        public string? FeedPassword { get; }
    }
}