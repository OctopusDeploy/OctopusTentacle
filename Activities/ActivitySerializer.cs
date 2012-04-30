using System;
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
            using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(gzip))
            using (var xmlWriter = new XmlTextWriter(writer))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.IndentChar = ' ';
                xmlWriter.Indentation = 2;

                var serializer = new XmlSerializer(typeof (ActivityElement));
                serializer.Serialize(xmlWriter, BuildTree(state));
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

                var i = 1;
                AssignId(result, ref i);

                return result;
            }
        }

        static void AssignId(ActivityElement result, ref int i)
        {
            result.Id = i;
            i++;
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
            
            if (state.Log != null)
            {
                element.Log = state.Log.ToString();
            }

            if (state.Error != null)
            {
                element.Error = state.Error.ToString();
            }
            
            element.Children = state.Children.AsParallel().Select(BuildTree).ToArray();
            
            return element;
        }
    }
}