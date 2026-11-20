using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Octopus.Pipelines.Runner.Gateway;

namespace Octopus.Pipelines.Runner.Tests;

public sealed class GatewayHttpHandlerTests
{
    [Fact]
    public void Client_certificate_is_offered_when_configured()
    {
        using var cert = CreateCertificate("client");

        using var handler = GatewayHttpHandler.Create(new GatewayOptions { ClientCertificate = cert });

        Assert.Contains(cert, handler.SslOptions.ClientCertificates!.Cast<X509Certificate2>());
    }

    [Fact]
    public void No_client_certificate_by_default()
    {
        using var handler = GatewayHttpHandler.Create(new GatewayOptions());

        Assert.True(handler.SslOptions.ClientCertificates is null || handler.SslOptions.ClientCertificates.Count == 0);
    }

    [Fact]
    public void Server_certificate_matching_the_validator_is_accepted()
    {
        using var trusted = CreateCertificate("server");
        using var handler = GatewayHttpHandler.Create(new GatewayOptions { ServerCertificateValidator = c => c.Thumbprint == trusted.Thumbprint });

        var callback = handler.SslOptions.RemoteCertificateValidationCallback!;

        Assert.True(callback(null!, trusted, null, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void Server_certificate_not_matching_the_validator_is_refused()
    {
        using var trusted = CreateCertificate("server");
        using var other = CreateCertificate("impostor");
        using var handler = GatewayHttpHandler.Create(new GatewayOptions { ServerCertificateValidator = c => c.Thumbprint == trusted.Thumbprint });

        var callback = handler.SslOptions.RemoteCertificateValidationCallback!;

        Assert.False(callback(null!, other, null, SslPolicyErrors.None));
        Assert.False(callback(null!, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));
    }

    [Fact]
    public void Default_validation_is_left_to_the_platform()
    {
        using var handler = GatewayHttpHandler.Create(new GatewayOptions());

        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }

    [Fact]
    public async Task Client_certificate_is_presented_in_the_TLS_handshake()
    {
        using var clientCert = CreateCertificate("client");
        using var serverCert = CreateCertificate("server");
        // Windows' SChannel needs a key it can use for TLS, so round-trip through PFX.
        using var serverIdentity = X509CertificateLoader.LoadPkcs12(serverCert.Export(X509ContentType.Pfx), null);
        using var clientIdentity = X509CertificateLoader.LoadPkcs12(clientCert.Export(X509ContentType.Pfx), null);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string? receivedThumbprint = null;

            var server = Task.Run(async () =>
            {
                using var tcp = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
                using var ssl = new SslStream(tcp.GetStream(), false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = serverIdentity,
                    ClientCertificateRequired = true,
                    RemoteCertificateValidationCallback = (_, cert, _, _) =>
                    {
                        receivedThumbprint = cert is null ? null : new X509Certificate2(cert).Thumbprint;
                        return true;
                    },
                }, TestContext.Current.CancellationToken);
                // Let the client send its request, then hang up.
                _ = await ssl.ReadAsync(new byte[1], TestContext.Current.CancellationToken);
            }, TestContext.Current.CancellationToken);

            using var handler = GatewayHttpHandler.Create(new GatewayOptions
            {
                ClientCertificate = clientIdentity,
                ServerCertificateValidator = c => c.Thumbprint == serverIdentity.Thumbprint,
            });
            using var http = new HttpClient(handler);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));

            // The server completes the handshake and hangs up; only the handshake matters.
            var request = http.GetAsync($"https://127.0.0.1:{port}/", timeout.Token);
            await server.WaitAsync(timeout.Token);

            // HttpClient retries the dropped request on a new connection nobody accepts, so stop it rather than wait.
            await timeout.CancelAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => request);

            Assert.Equal(clientIdentity.Thumbprint, receivedThumbprint);
        }
        finally
        {
            listener.Stop();
        }
    }

    static X509Certificate2 CreateCertificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
