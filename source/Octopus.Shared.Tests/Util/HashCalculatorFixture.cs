using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Util
{
    [TestFixture]
    public class HashCalculatorFixture
    {
        const string TextToHash =
            "No one would have believed in the last years of the nineteenth century that this world was being watched keenly and closely by intelligences greater than man's and yet as mortal as his own; that as men busied themselves about their various concerns they were scrutinised and studied, perhaps almost as narrowly as a man with a microscope might scrutinise the transient creatures that swarm and multiply in a drop of water. With infinite complacency men went to and fro over this globe about their little affairs, serene in their assurance of their empire over matter. It is possible that the infusoria under the microscope do the same. No one gave a thought to the older worlds of space as sources of human danger, or thought of them only to dismiss the idea of life upon them as impossible or improbable. It is curious to recall some of the mental habits of those departed days. At most terrestrial men fancied there might be other men upon Mars, perhaps inferior to themselves and ready to welcome a missionary enterprise. Yet across the gulf of space, minds that are to our minds as ours are to those of the beasts that perish, intellects vast and cool and unsympathetic, regarded this earth with envious eyes, and slowly and surely drew their plans against us. And early in the twentieth century came the great disillusionment.";

        const string HashOfAbove = "81bb6ffd615914816eb0ada3c3dadb9f50dd390a";

        [Test]
        public void ShouldComputeDifferentHashBasedOnInput()
        {
            var a = HashCalculator.Hash(Stream("Apple"));
            var b = HashCalculator.Hash(Stream("Applf"));
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void ShouldComputeSameHashBasedOnInput()
        {
            var a = HashCalculator.Hash(Stream("Apple"));
            var b = HashCalculator.Hash(Stream("Apple"));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ShouldComputeHashPredictably()
        {
            var hash = HashCalculator.Hash(Stream(TextToHash));
            Assert.AreEqual(hash, HashOfAbove);
        }

        Stream Stream(string input)
            => new MemoryStream(Encoding.UTF8.GetBytes(input), false);
    }
}