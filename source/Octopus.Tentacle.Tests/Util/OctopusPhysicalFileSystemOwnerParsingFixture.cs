using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Util
{
    [TestFixture]
    public class OctopusPhysicalFileSystemOwnerParsingFixture
    {
        [TestCase("-rw-r--r-- 1 1000 1000 42 Jan  1 00:00 /etc/octopus/t/tentacle.config", "1000:1000", Description = "GNU")]
        [TestCase("-rw-r--r--  1 501  20  42  1 Jan 00:00 /etc/octopus/t/tentacle.config", "501:20", Description = "BSD/macOS")]
        [TestCase("-rw-r--r--    1 0        0               42 Jan  1 00:00 tentacle.config\n", "0:0", Description = "BusyBox, trailing newline")]
        public void ParsesUidAndGidFromLsNumericOutput(string lsOutput, string expected)
            => OctopusPhysicalFileSystem.ParseNumericOwnerFromLs(lsOutput).Should().Be(expected);

        [TestCase("")]
        [TestCase("ls: cannot access 'nope': No such file or directory")]
        [TestCase("-rw-r--r-- 1 tentacle tentacle 42 Jan 1 00:00 file", Description = "names, not numbers")]
        public void ReturnsNullWhenTheLineIsNotNumericLsOutput(string lsOutput)
            => OctopusPhysicalFileSystem.ParseNumericOwnerFromLs(lsOutput).Should().BeNull();
    }
}
