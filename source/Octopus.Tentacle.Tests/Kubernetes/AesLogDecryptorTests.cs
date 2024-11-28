using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class AesLogDecryptorTests
    {
        [TestCase("531f69408fcc09129a42b46b93c7c14fe7c36fec74ac77929b4e1f29b6b0c1e7cfe78055ceee24fbca5f9097501b5cb548f78928b5","##octopus[stdout-verbose]")]
        [TestCase("4463ba9b0672e65fdabcd99681ee20e5f003a59ce2c1b1751303e6399d5515ae4a715ed47ce5b7c5727ca66a127e485cd22de2d1dad76d8f704922dae0036d99c4a7e151498043b9d8","EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>0")]
        public void ValidKey_ShouldProduceCOrrectOutput(string encryptedMessage, string expectedDecryptedMessage)
        {
            var key = Encoding.UTF8.GetBytes("qwertyuioplkjhgfdsazxcvbnmqwertd");
            
            var result = AesLogDecryptor.DecryptLogMessage(encryptedMessage, key);

            result.Should().Be(expectedDecryptedMessage);
        }
    }
}