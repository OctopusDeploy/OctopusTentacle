using System;
using NUnit.Framework;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Tests.Diagnostics
{
    [TestFixture]
    public class SensitiveValueMaskerFixture
    {
        [Test]
        public void ASensitiveValueIsMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value",
                expected = "This contains a ******** value";

            var masker = new SensitiveValueMasker(sensitiveValues: new[] { sensitive });
            string? result = null;
            masker.SafeSanitize(raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void CanAddSensitiveValueToExistingContext()
        {
            const string sensitive = "sensitive",
                anotherSensitive = "&3avh3#dhe@",
                raw = "This contains a sensitive value and &3avh3#dhe@",
                expected = "This contains a ******** value and ********";

            var masker = new SensitiveValueMasker(sensitiveValues: new[] { sensitive });
            string? result = null;
            masker.SafeSanitize(raw, sanitized => { });

            masker.WithSensitiveValue(anotherSensitive);
            masker.SafeSanitize(raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }
    }
}