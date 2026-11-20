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
            // Without this, .NET drops a certificate whose issuer isn't in the server's accepted CA list, and a
            // self-signed one never is. Always offer ours, as Halibut does.
            handler.SslOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCertificate;
        }

        if (options.ServerCertificateValidator is { } validator)
        {
            // Chain and name errors are ignored on purpose: the certificate's thumbprint is what's trusted, as with Halibut.
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null)
                {
                    return false;
                }

                // The server's certificate is usually already an X509Certificate2; only copy (and dispose) when it isn't.
                if (certificate is X509Certificate2 certificate2)
                {
                    return validator(certificate2);
                }

                using var copy = new X509Certificate2(certificate);
                return validator(copy);
            };
        }

        return handler;
    }
}
