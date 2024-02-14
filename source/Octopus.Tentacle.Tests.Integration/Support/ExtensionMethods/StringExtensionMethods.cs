using System.Text;

namespace Octopus.Tentacle.Tests.Integration.Support.ExtensionMethods
{
    public static class StringExtensionMethods
    {
        public static byte[] GetUTF8Bytes(this string s)
        {
            return Encoding.UTF8.GetBytes(s);
        }
    }
}
