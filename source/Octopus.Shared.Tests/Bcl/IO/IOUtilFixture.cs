using System.IO;
using System.Text;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Bcl.IO
{
    // ReSharper disable once InconsistentNaming
    [TestFixture]
    public class IOUtilFixture
    {
        static Encoding Sniff(string filename)
        {
            var path = Path.Combine(
                Path.GetDirectoryName(typeof (IOUtilFixture).Assembly.Location) ?? ".",
                "Bcl",
                "IO",
                filename);

            var fs = new OctopusPhysicalFileSystem();
            return IOUtil.SniffEncoding(fs, path);
        }

        [Test]
        public void DetectsUtf8WithBom()
        {
            var sniffed = Sniff("UTF8-BOM.txt");
            Assert.IsInstanceOf<UTF8Encoding>(sniffed);
            Assert.AreEqual(3, sniffed.GetPreamble().Length);
        }

        [Test]
        public void DetectsUtf8WithoutBom()
        {
            var sniffed = Sniff("UTF8-no-BOM.txt");
            Assert.IsInstanceOf<UTF8Encoding>(sniffed);
            Assert.AreEqual(0, sniffed.GetPreamble().Length);
        }

        [Test]
        public void DetectsNonUtf8()
        {
            var sniffed = Sniff("UTF16.txt");
            Assert.IsInstanceOf<UnicodeEncoding>(sniffed);
        }

        [Test]
        public void DefaultsToUtf8NoBom()
        {
            var sniffed = Sniff("Zero.txt");
            Assert.IsInstanceOf<UTF8Encoding>(sniffed);
            Assert.AreEqual(0, sniffed.GetPreamble().Length);
        }
    }
}