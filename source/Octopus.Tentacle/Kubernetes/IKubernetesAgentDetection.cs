using System;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Tentacle.Kubernetes;

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
/// Can be used with dependency injection via <see cref="IKubernetesAgentDetection"/> or statically
/// </summary>
public class KubernetesAgentDetection : IKubernetesAgentDetection
{
    public static bool IsRunningAsKubernetesAgent => !string.IsNullOrWhiteSpace(Namespace);
    public static string? Namespace => Environment.GetEnvironmentVariable(EnvironmentKubernetesConfiguration.VariableNames.NamespaceVariableName);
        
    /// <inheritdoc/>
    bool IKubernetesAgentDetection.IsRunningAsKubernetesAgent => IsRunningAsKubernetesAgent;
        
    /// <inheritdoc/>
    string? IKubernetesAgentDetection.Namespace => Namespace;
}