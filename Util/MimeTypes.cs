using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Octopus.Shared.Util
{
    /// <summary>
    /// MimeType to/from file extension mappings
    /// </summary>
    /// <remarks>
    /// Using System.Web or Nancy a more complete set could be created, or we can
    /// add mappings here as needed.
    /// </remarks>
    public class MimeType
    {
        public static string ForExtension(string extension) 
        {
            switch (extension.ToLowerInvariant().TrimStart('.'))
            {
                case "png":
                    return "image/png";
                case "jpg":
                    return "image/jpg";
                default:
                    throw new Exception("Mimetype not currently mapped for extension '" + extension + "'");
            }
        }

        public static string ToExtension(string mimeType)
        {
            switch (mimeType.ToLowerInvariant())
            {
                case "image/png":
                    return ".png";
                case "image/jpg":
                case "image/jpeg":
                    return ".jpg";
                default:
                    throw new Exception("Mimetype not currently mapped: '" + mimeType + "'");
            }
        }
    }; 
}