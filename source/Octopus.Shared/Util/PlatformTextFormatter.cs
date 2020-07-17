using System;
using System.IO;

namespace Octopus.Shared.Util
{
    public static class PlatformTextFormatter
    {
        public const string UnixNewLine = "\n";
        public const string WindowsNewLine = "\r\n";

        public static void CopyWindowsToUnix(Stream windows, Stream unix)
        {
            CopyText(windows, unix, UnixNewLine);
        }

        public static void CopyUnixToWindows(Stream unix, Stream windows)
        {
            CopyText(unix, windows, WindowsNewLine);
        }

        // This'll need to be refined to not add trailing newlines and so-on
        static void CopyText(Stream source, Stream destination, string destinationLineEnding)
        {
            using (var file = new StreamReader(source))
            using (var uploadStream = new StreamWriter(destination))
            {
                uploadStream.NewLine = destinationLineEnding;
                var content = file.ReadLine();
                while (content != null)
                {
                    uploadStream.WriteLine(content);

                    content = file.ReadLine();
                }
            }
        }

        public static string? ToUnixString(string windows)
        {
            if (windows == null) return null;

            return windows.Replace(WindowsNewLine, UnixNewLine);
        }

        // Also needs refinement - if a CRLF string is passed it will turn into CRCRLF
        public static string? ToWindowsString(string unix)
        {
            if (unix == null) return null;

            return unix.Replace(UnixNewLine, WindowsNewLine);
        }
    }
}