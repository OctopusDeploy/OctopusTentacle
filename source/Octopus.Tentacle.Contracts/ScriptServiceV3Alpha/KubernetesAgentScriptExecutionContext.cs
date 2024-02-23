using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.ScriptServiceV3Alpha
{
    public class KubernetesAgentScriptExecutionContext : IScriptExecutionContext
    {
        [JsonConstructor]
        public KubernetesAgentScriptExecutionContext(string image, string? feedUrl, string? feedUsername, string? feedPassword)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public KubernetesAgentScriptExecutionContext()
        {
        }

        public string? Image { get; }

        public string? FeedUrl { get; }

        public string? FeedUsername { get; }

        public string? FeedPassword { get; }
    }
}