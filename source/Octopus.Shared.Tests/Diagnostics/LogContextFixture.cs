using NUnit.Framework;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Tests.Diagnostics
{
    [TestFixture]
    public class LogContextFixture
    {
        [Test]
        public void ASensitiveValueIsMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value",
                expected = "This contains a ******** value";

            var logContext = new LogContext(sensitiveValues: new[] { sensitive });
            string result = null;
            logContext.SafeSanitize(raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ChildContextUsesParentsSensitiveMask()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value",
                expected = "This contains a ******** value";

            var logContext = new LogContext(sensitiveValues: new[] { sensitive });
            var childContext = logContext.CreateChild();
            string result = null;
            childContext.SafeSanitize(raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ChildContextCanHaveOwnSensitiveValues()
        {
            const string sensitive = "sensitive",
                childSensitive = "child",
                raw = "This contains a sensitive child value",
                expected = "This contains a ******** child value",
                expectedChild = "This contains a ******** ******** value";

            var logContext = new LogContext(sensitiveValues: new[] { sensitive });
            var childContext = logContext.CreateChild(new[] { childSensitive });
            string result = null;

            logContext.SafeSanitize(raw, sanitized => result = sanitized);
            Assert.AreEqual(expected, result);

            childContext.SafeSanitize(raw, sanitized => result = sanitized);
            Assert.AreEqual(expectedChild, result);
        }

        [Test]
        public void ChainedChildrenCombineSensitiveValues()
        {
            const string sensitive1 = "sensitive",
                sensitive2 = "value",
                raw = "This contains a sensitive value",
                expected = "This contains a ******** ********";

            var logContext = new LogContext();
            var childContext = logContext
                .CreateChild(new[] { sensitive1 })
                .CreateChild(new[] { sensitive2 });
            string result = null;

            childContext.SafeSanitize(raw, sanitized => result = sanitized);
            Assert.AreEqual(expected, result);
        }
    }
}