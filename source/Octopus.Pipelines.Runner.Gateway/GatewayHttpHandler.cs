namespace Octopus.Pipelines.Runner.Gateway;

static class GatewayHttpHandler
{
    public static SocketsHttpHandler Create() => new()
    {
        // Keep the stream alive through Azure Load Balancer's idle timeout (4 minutes by default).
        KeepAlivePingDelay = TimeSpan.FromSeconds(30),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(20),
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        EnableMultipleHttp2Connections = true,
    };
}
