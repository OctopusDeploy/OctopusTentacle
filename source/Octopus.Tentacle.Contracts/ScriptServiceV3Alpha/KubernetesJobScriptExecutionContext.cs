using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class KubernetesJobScriptExecutionContext : IScriptExecutionContext
    {
        [JsonConstructor]
        public KubernetesJobScriptExecutionContext(string image, string feedUrl, string feedUsername, string feedPassword)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public KubernetesJobScriptExecutionContext()
        {
        }

        public string? Image { get; }

        public string? FeedUrl { get; }

        public string? FeedUsername { get; }

        public string? FeedPassword { get; }
    }
}