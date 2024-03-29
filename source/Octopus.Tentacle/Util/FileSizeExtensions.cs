﻿using System;

namespace Octopus.Tentacle.Util
{
    public static class FileSizeExtensions
    {
        public static string ToFileSizeString(this long bytes)
            => ToFileSizeString(bytes <= 0 ? 0 : (ulong)bytes);

        // Returns the human-readable file size for an arbitrary, 64-bit file size.
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB".
        public static string ToFileSizeString(this ulong i)
        {
            // Determine the suffix and readable value.
            string suffix;
            double readable;
            if (i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = i >> 50;
            }
            else if (i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = i >> 40;
            }
            else if (i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = i >> 30;
            }
            else if (i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = i >> 20;
            }
            else if (i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = i >> 10;
            }
            else if (i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }

            // Divide by 1024 to get fractional value.
            readable = readable / 1024;

            // Return formatted number with suffix.
            return readable.ToString("0.### ") + suffix;
        }
    }
}