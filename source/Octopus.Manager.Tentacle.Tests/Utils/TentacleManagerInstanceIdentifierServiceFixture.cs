using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.Utils
{
    public class TentacleManagerInstanceIdentifierServiceFixture
    {
        readonly string identifierLocation = Path.GetTempPath();
        
        [Test]
        public async Task Given_ThereIsNoExistingIdentifier_When_AnIdentifierIsRequested_Then_ANewIdentifierIsGeneratedAndReturned()
        {
            // Arrange: Ensure there is no current identifier
            var identifierFullPath = Path.Combine(identifierLocation, TentacleManagerInstanceIdentifierService.IdentifierFileName);
            File.Delete(identifierFullPath);
            Assert.False(File.Exists(identifierFullPath));

            // Act: Get the identifier
            var sut = new TentacleManagerInstanceIdentifierService(new DirectoryInfo(identifierLocation));
            var identifierValue = await sut.GetIdentifier();
            
            // Assert: The identifier returned should match the contents of the file we expect to be created
            Assert.True(File.Exists(identifierFullPath));
            var identifierFileContents = File.ReadAllLines(identifierFullPath);
            Assert.AreEqual(1, identifierFileContents.Length);
            Assert.AreEqual(identifierValue, identifierFileContents.Single());
        }

        [Test]
        public async Task Given_ThereIsAnExistingIdentifier_When_AnIdentifierIsRequested_Then_TheExistingIdentifierIsReturned()
        {
            // Arrange: Ensure there is an existing identifier
            var identifierFullPath = Path.Combine(identifierLocation, TentacleManagerInstanceIdentifierService.IdentifierFileName);
            var identifier = Guid.NewGuid().ToString("N");
            // ReSharper disable once ConvertToUsingDeclaration
            using (var streamWriter = File.CreateText(identifierFullPath))
            {
                await streamWriter.WriteLineAsync(identifier);
            }

            // Act: Get the identifier
            var sut = new TentacleManagerInstanceIdentifierService(new DirectoryInfo(identifierLocation));
            var identifierValue = await sut.GetIdentifier();
            
            // Assert: The identifier returned should match the contents of the pre-existing file
            Assert.AreEqual(identifier, identifierValue);
        }

        [Test]
        public async Task Given_ThereIsAnExistingInvalidIdentifier_When_AnIdentifierIsRequested_Then_ANewIdentifierIsGeneratedAndReturned()
        {
            // Arrange: Ensure there is an existing but invalid identifier file
            var identifierFullPath = Path.Combine(identifierLocation, TentacleManagerInstanceIdentifierService.IdentifierFileName);
            var invalidLine1 = "This is not a valid ID string";
            var invalidLine2 = "This contains multiple lines";
            // ReSharper disable once ConvertToUsingDeclaration
            using (var streamWriter = File.CreateText(identifierFullPath))
            {
                await streamWriter.WriteLineAsync(invalidLine1);
                await streamWriter.WriteLineAsync(invalidLine2);
            }
            
            // Act: Get the identifier
            var sut = new TentacleManagerInstanceIdentifierService(new DirectoryInfo(identifierLocation));
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
    }
}
