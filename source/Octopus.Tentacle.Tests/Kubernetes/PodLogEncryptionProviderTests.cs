using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Crypto;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class PodLogEncryptionProviderTests
    {
        IPodLogEncryptionProvider sut;
        
        public static readonly byte[] Nonce =
        {
            12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1
        };
        
        [SetUp]
        public void SetUp()
        {
            var key = Encoding.UTF8.GetBytes("qwertyuioplkjhgfdsazxcvbnmqwertd");
            sut = PodLogEncryptionProvider.Create(key);
        }
        
        [TestCase("531f69408fcc09129a42b46b93c7c14fe7c36fec74ac77929b4e1f29b6b0c1e7cfe78055ceee24fbca5f9097501b5cb548f78928b5", "##octopus[stdout-verbose]")]
        [TestCase("4463ba9b0672e65fdabcd99681ee20e5f003a59ce2c1b1751303e6399d5515ae4a715ed47ce5b7c5727ca66a127e485cd22de2d1dad76d8f704922dae0036d99c4a7e151498043b9d8", "EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>0")]
        public void Decrypt_ShouldProduceCorrectOutput(string encryptedMessage, string expectedDecryptedMessage)
        {
            var result = sut.Decrypt(encryptedMessage);

            result.Should().Be(expectedDecryptedMessage);
        }

        [TestCase("a cool message", "0c0b0a090807060504030201416ce819a896623cee784f92807857b9be36bd6075461789031a3d29aef1")]
        [TestCase("##octopus[stdout-verbose]", "0c0b0a090807060504030201036fe415b3953224f8504f8783723ca65dca38cd3eab365c4d10d4efad161112e96cd449c6a52348ef")]
        [TestCase("EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>0", "0c0b0a0908070605040302016503d85bf7cd7712cf3f7ac3ca250ae5469169866d80687b51b3cede4af296732090b9a9577055be3ced266ceb9eecd094a74f8df65c95ab33d9f5170c")]
        public void Encrypt_ShouldProduceCorrectOutput(string plaintextMessage, string expectedEncryptedMessage)
        {
            var result = sut.Encrypt(plaintextMessage, Nonce);

            result.Should().Be(expectedEncryptedMessage);
        }
    }
}