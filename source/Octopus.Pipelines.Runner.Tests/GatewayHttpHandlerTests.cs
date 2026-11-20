using System.Net.Security;
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

    static X509Certificate2 CreateCertificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
