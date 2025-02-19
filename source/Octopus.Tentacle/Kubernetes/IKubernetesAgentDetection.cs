using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesAgentDetection
    {
        /// <summary>
        /// Indicates if the Tentacle is running inside a Kubernetes cluster as the Kubernetes Agent
        /// </summary>
        [MemberNotNullWhen(true, nameof(Namespace))]
        bool IsRunningAsKubernetesAgent { get; }
        
        /// <summary>
        /// The Kubernetes namespace the agent is running under, <code>null</code> if not running as a Kubernetes agent
        /// </summary>
        string? Namespace { get; }
    }

    /// <summary>
    /// Used for detection if the tentacle is running as part of the Kubernetes agent helm chart
    /// Can be used with dependency injection via <see cref="IKubernetesAgentDetection"/> or statically (in scenarios where dependency injection isn't available)
    /// </summary>
    public class KubernetesAgentDetection : IKubernetesAgentDetection
    {
        /// <summary>
        /// Indicates if the Tentacle is running inside a Kubernetes cluster as the Kubernetes Agent
        /// </summary>
        [MemberNotNullWhen(true, nameof(Namespace))]
        public static bool IsRunningAsKubernetesAgent => !string.IsNullOrWhiteSpace(Namespace);
               
        /// <summary>
        /// The Kubernetes namespace the Kubernetes Agent is running under, <code>null</code> if not running as a Kubernetes agent
        /// </summary>
        public static string? Namespace => Environment.GetEnvironmentVariable(EnvironmentKubernetesConfiguration.VariableNames.Namespace);
        
        /// <inheritdoc/>
        bool IKubernetesAgentDetection.IsRunningAsKubernetesAgent => IsRunningAsKubernetesAgent;
        
        /// <inheritdoc/>
        string? IKubernetesAgentDetection.Namespace => Namespace;
    }
}