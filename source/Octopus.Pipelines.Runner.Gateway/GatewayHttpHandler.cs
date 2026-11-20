using System.Security.Cryptography.X509Certificates;

namespace Octopus.Pipelines.Runner.Gateway;

static class GatewayHttpHandler
{
    public static SocketsHttpHandler Create(GatewayOptions options)
    {
        var handler = new SocketsHttpHandler
        {
            // Keep the stream alive through Azure Load Balancer's idle timeout (4 minutes by default).
            KeepAlivePingDelay = TimeSpan.FromSeconds(30),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(20),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            EnableMultipleHttp2Connections = true,
        };

        if (options.ClientCertificate is { } clientCertificate)
        {
            handler.SslOptions.ClientCertificates = [clientCertificate];
        }

        if (options.ServerCertificateValidator is { } validator)
        {
            // Chain and name errors are ignored on purpose: the certificate's thumbprint is what's trusted, as with Halibut.
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                certificate is not null && validator(new X509Certificate2(certificate));
        }

        return handler;
    }
}
