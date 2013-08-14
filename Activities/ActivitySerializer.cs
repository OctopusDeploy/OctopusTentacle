using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Shared.Activities
{
    public class ActivitySerializer
    {
        public static ActivityElement Deserialize(Stream serialized)
        {
            using (var gzip = new GZipStream(serialized, CompressionMode.Decompress))
            using (var text = new StreamReader(gzip))
            using (var xmlTextReader = new XmlTextReader(text))
            {
                var serializer = new XmlSerializer(typeof(ActivityElement));
                var result = (ActivityElement)serializer.Deserialize(xmlTextReader);

                // Old versions of Octopus didn't persist an ID, so we have to generate a unique ID each time
                var i = 1;
                AssignId(result, ref i);

                return result;
            }
        }

        static void AssignId(ActivityElement result, ref int i)
        {
            if (result.Id == null || result.Id == "0")
            {
                result.Id = i.ToString(CultureInfo.InvariantCulture);
                i++;
            }

            if (result.Children == null)
                return;

            foreach (var child in result.Children)
            {
                AssignId(child, ref i);
            }
        }
    }
}