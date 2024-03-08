using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Halibut.Util;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.Utils
{
    public class TentacleManagerInstanceIdentifierServiceFixture
    {
        [Test]
        public async Task Given_ThereIsNoExistingIdentifier_When_AnIdentifierIsRequested_Then_ANewIdentifierIsGeneratedAndReturned()
        {
            var identifierFullPath = Path.GetTempFileName();
            try
            {
                // Arrange: Ensure there is no current identifier
                File.Delete(identifierFullPath);
                Assert.False(File.Exists(identifierFullPath));

                // Act: Get the identifier
                var sut = new TentacleManagerInstanceIdentifierService(identifierFullPath);
                var identifierValue = await sut.GetIdentifier();

                // Assert: The identifier returned should match the contents of the file we expect to be created
                Assert.IsNotNull(identifierValue);
                Assert.IsNotEmpty(identifierValue);
                Assert.True(File.Exists(identifierFullPath));
                var identifierFileContents = File.ReadAllLines(identifierFullPath);
                Assert.AreEqual(1, identifierFileContents.Length);
                Assert.AreEqual(identifierValue, identifierFileContents.Single());
            }
            finally
            {
                File.Delete(identifierFullPath);
            }
        }

        [Test]
        public async Task Given_ThereIsAnExistingIdentifier_When_AnIdentifierIsRequested_Then_TheExistingIdentifierIsReturned()
        {
            var identifierFullPath = Path.GetTempFileName();
            try
            {
                // Arrange: Ensure there is an existing identifier
                var identifier = Guid.NewGuid().ToString("N");
                File.WriteAllLines(identifierFullPath, new[] {identifier});

                // Act: Get the identifier
                var sut = new TentacleManagerInstanceIdentifierService(identifierFullPath);
                var identifierValue = await sut.GetIdentifier();

                // Assert: The identifier returned should match the contents of the pre-existing file
                Assert.AreEqual(identifier, identifierValue);
            }
            finally
            {
                File.Delete(identifierFullPath);
            }
        }

        [Test]
        public async Task Given_ThereIsAnExistingInvalidIdentifier_When_AnIdentifierIsRequested_Then_ANewIdentifierIsGeneratedAndReturned()
        {
            var identifierFullPath = Path.GetTempFileName();
            try
            {
                // Arrange: Ensure there is an existing but invalid identifier file
                var invalidLine1 = "This is not a valid ID string";
                var invalidLine2 = "This contains multiple lines";
                File.WriteAllLines(identifierFullPath, new[] {invalidLine1, invalidLine2});

                // Act: Get the identifier
                var sut = new TentacleManagerInstanceIdentifierService(identifierFullPath);
                var identifierValue = await sut.GetIdentifier();

                // Assert: The identifier returned should be a valid ID
                Assert.DoesNotThrow(() => Guid.Parse(identifierValue));
                // Assert: The identifier file should not contain the original invalid value(s)
                var identifierFileContents = File.ReadAllLines(identifierFullPath);
                Assert.AreEqual(1, identifierFileContents.Length);
                var identifierFileContentsLine = identifierFileContents.Single();
                Assert.IsFalse(identifierFileContentsLine.Contains(invalidLine1));
                Assert.IsFalse(identifierFileContentsLine.Contains(invalidLine2));
            }
            finally
            {
                File.Delete(identifierFullPath);
            }
        }
    }
}
