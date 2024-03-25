namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class KubernetesImageConfiguration
    {
        public KubernetesImageConfiguration(string image, string? feedUrl, string? feedUsername, string? feedPassword)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public string Image { get; }
        public string? FeedUrl { get; }
        public string? FeedUsername { get; }
        public string? FeedPassword { get; }
    }
}