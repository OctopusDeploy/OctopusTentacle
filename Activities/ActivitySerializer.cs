using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Shared.Activities
{
    public class ActivitySerializer
    {
        public static void Serialize(IActivityState state, Stream output)
        {
            Serialize(state, output, null);
        }

        public static void Serialize(IActivityState state, Stream output, ActivityElement originalLog)
        {
            using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(gzip))
            using (var xmlWriter = new XmlTextWriter(writer))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.IndentChar = ' ';
                xmlWriter.Indentation = 2;

                var newElement = BuildTree(state);
                if (originalLog != null)
                {
                    var clone = originalLog.Clone();

                    var fullLog = clone.Children.ToList();
                    fullLog.AddRange(newElement.Children);
                    clone.Children = fullLog.ToArray();
                    clone.Log += Environment.NewLine + newElement.Log;
                    newElement = clone;
                }

                var serializer = new XmlSerializer(typeof(ActivityElement));
                serializer.Serialize(xmlWriter, newElement);
            }
        }

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

        static ActivityElement BuildTree(IActivityState state)
        {
            var element = new ActivityElement();
            element.Name = state.Name;
            element.Status = state.Status;
            element.Tag = state.Tag;
            element.Id = state.Id;
            
            if (state.Log != null)
            {
                element.Log = state.Log.GetLog();
            }

            if (state.Error != null)
            {
                element.Error = state.Error.ToString();
            }
            
            element.Children = state.Children.Select(BuildTree).ToArray();
            
            return element;
        }
    }
}