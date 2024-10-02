using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Kubernetes
{
    public record KubernetesAgentToolsImageVersionMetadata(
        [JsonProperty("tools")] KubernetesAgentToolVersions ToolVersions,
        [JsonProperty("latest")] Version Latest,
        [JsonProperty("revisionHash")] string RevisionHash,
        [JsonProperty("deprecations")] Dictionary<Version, KubernetesAgentToolDeprecation> Deprecations);

    public record KubernetesAgentToolVersions(
        [JsonProperty("kubectl")] List<Version> Kubectl,
        [JsonProperty("helm")] List<Version> Helm,
        [JsonProperty("powershell")] List<Version> Powershell
    );

    public record KubernetesAgentToolDeprecation(
        [JsonProperty("latestTag")] string LatestTag);
}