using Grpc.Net.Client;

namespace Octopus.Pipelines.Runner.Gateway;

/// <summary>
/// Opens the gRPC channel that remote hosts (Tentacle, the Kubernetes runner) give <see cref="GatewayClient"/> as its
/// transport. Kept apart from the client so a host that never opens a channel, like the CLI, doesn't ship the
/// HTTP/2 stack: with nothing calling this, Native AOT trims it away.
/// </summary>
public static class GatewayChannel
{
    /// <summary>A channel to <see cref="GatewayOptions.ServerAddress"/>. The caller disposes it after the client.</summary>
    public static GrpcChannel Create(GatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return GrpcChannel.ForAddress(options.ServerAddress, new GrpcChannelOptions { HttpHandler = GatewayHttpHandler.Create() });
    }
}
