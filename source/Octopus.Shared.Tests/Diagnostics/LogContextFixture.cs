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
        public void ChildContextUsesParentsMultilineSensitiveMask()
        {
            const string sensitive = "multiline\nsensitive",
                expected = "This contains a **************** value";

            string[] raw = new[] { "This contains a multiline", "sensitive value" };

            var logContext = new LogContext();
            var childContext = logContext.CreateChild(new[] { sensitive });
            string result = "";
            foreach (var line in raw)
                childContext.SafeSanitize(line, sanitized => result += sanitized);

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

        [Test]
        public void ChildContextsKeepSearchStateOnDifferentThreads()
        {
            const string sensitive = "multi\nline\nsecret",
                childSensitive = "child\nsecret";

            string[] raw = { "This has a multi", "line", "secret agent with a child", "secret agent" };

            var parentContext = new LogContext(sensitiveValues: new[] { sensitive });
            var childContext1 = parentContext.CreateChild(new[] { childSensitive });
            var childContext2 = parentContext.CreateChild(new[] { childSensitive });
            string result1 = "", result2 = "";

            foreach (var line in raw)
            {
                childContext1.SafeSanitize(line, sanitized => result1 += sanitized);
                childContext2.SafeSanitize(line, sanitized => result2 += sanitized);
            }

            Assert.AreEqual("This has a **************** agent with a **************** agent", result1);
            Assert.AreEqual("This has a **************** agent with a **************** agent", result2);
        }
        
        [Test]
        public void CanAddSensitiveValueToExistingContext()
        {
            const string sensitive = "sensitive",
                anotherSensitive = "&3avh3#dhe@",
                raw = "This contains a sensitive value and &3avh3#dhe@",
                expected = "This contains a ******** value and ********";

            var logContext = new LogContext(sensitiveValues: new[] { sensitive });
            string result = null;
            logContext.SafeSanitize(raw, sanitized => {});
            
            logContext.WithSensitiveValue(anotherSensitive);
            logContext.SafeSanitize(raw, sanitized => result = sanitized );
            
            Assert.AreEqual(expected, result);
        }
    }
}