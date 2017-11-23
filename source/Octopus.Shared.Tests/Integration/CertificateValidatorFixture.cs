#if HAS_SYSTEM_IDENTITYMODEL_TOKENS
using System.IdentityModel.Tokens;
#else
using SecurityTokenValidationException = System.Exception;
#endif
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security;

namespace Octopus.Shared.Tests.Integration
{
    [TestFixture]
    public class CertificateValidatorFixture
    {
        readonly CertificateGenerator generator = new CertificateGenerator();

        [Test]
        public void AcceptsValidCertificate()
        {
            var expected = generator.GenerateNew("CN=expected", new NullLog());

            var validator = new CertificateValidator(() => new[] {expected.Thumbprint}, CertificateValidationDirection.TheyCalledUs, Substitute.For<ILog>());

            validator.Validate(expected);
        }

        [Test]
        public void RejectsInvalidCertificate()
        {
            var expected = generator.GenerateNew("CN=expected", new NullLog());
            var evil = generator.GenerateNew("CN=evil", new NullLog());

            var validator1 = new CertificateValidator(() => new[] {expected.Thumbprint}, CertificateValidationDirection.TheyCalledUs, Substitute.For<ILog>());

            Assert.Throws<SecurityTokenValidationException>(() => validator1.Validate(evil));

            var validator2 = new CertificateValidator(() => new[] {expected.Thumbprint}, CertificateValidationDirection.WeCalledThem, Substitute.For<ILog>());

            Assert.Throws<SecurityTokenValidationException>(() => validator2.Validate(evil));
        }
    }
}