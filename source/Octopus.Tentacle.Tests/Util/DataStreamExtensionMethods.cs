using System.IO;
using System.Text;
using Halibut;

namespace Octopus.Tentacle.Tests.Util
{
    public static class DataStreamExtensionMethods
    {
        public static byte[] ToBytes(this DataStream dataStream)
        {
            byte[] bytes = null;
            dataStream.Receiver()
                .Read(
                    stream =>
                    {
                        if (stream is MemoryStream ms)
                        {
                            bytes = ms.ToArray();
                        }
                        else
                        {
                            using var memoryStream = new MemoryStream();

                            stream.CopyTo(memoryStream);
                            bytes = memoryStream.ToArray();
                        }
                    });

            return bytes;
        }

        public static string GetString(this DataStream dataStream, Encoding encoding) => encoding.GetString(dataStream.ToBytes());
    }
}
