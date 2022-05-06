using System;
using System.IO;
using System.Text;

namespace Octopus.Shared.Util
{
    // Resharper gets this wrong, treating the name as if the type's an interface.
    // ReSharper disable once InconsistentNaming
    public static class IOUtil
    {
        public static Encoding SniffEncoding(IOctopusFileSystem fileSystem, string path)
        {
            using (var file = fileSystem.OpenFile(path, FileAccess.Read))
            {
                if (file.Length == 0)
                    return new UTF8Encoding(false);

                using (var reader = new StreamReader(file, true))
                {
                    reader.ReadLine();
                    var encoding = reader.CurrentEncoding;
                    if (!(encoding is UTF8Encoding)) return encoding;

                    if (file.Length >= 3)
                    {
                        var threeBytes = new byte[3];
                        file.Position = 0;
                        if (file.Read(threeBytes, 0, 3) == 3)
                            if (threeBytes[0] == 0xEF &&
                                threeBytes[1] == 0xBB &&
                                threeBytes[2] == 0xBF)
                                return encoding;
                    }

                    return new UTF8Encoding(false);
                }
            }
        }
    }
}