using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesAgentDetection
    {
        /// <summary>
        /// Indicates if the Tentacle is running inside a Kubernetes cluster as the Kubernetes Agent
        /// </summary>
        bool IsRunningAsKubernetesAgent { get; }
        
        /// <summary>
        /// The Kubernetes namespace the agent is running under, <code>null</code> if not running as a Kubernetes agent
        /// </summary>
        [MemberNotNullWhen(true, nameof(Namespace))]
        string? Namespace { get; }
    }

    public class KubernetesAgentDetection : IKubernetesAgentDetection
    {
        public static bool IsRunningAsKubernetesAgent => !string.IsNullOrWhiteSpace(Namespace);
        public static string? Namespace => Environment.GetEnvironmentVariable(KubernetesEnvironmentVariableNames.Namespace);
        
        /// <inheritdoc/>
        bool IKubernetesAgentDetection.IsRunningAsKubernetesAgent => IsRunningAsKubernetesAgent;
        
        /// <inheritdoc/>
        string? IKubernetesAgentDetection.Namespace => Namespace;
    }
}