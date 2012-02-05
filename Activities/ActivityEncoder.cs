using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Shared.Activities
{
    public class ActivityEncoder
    {
        public static string RenderLog(IActivityHandle handle)
        {
            using (var text = new StringWriter())
            using (var xmlWriter = new XmlTextWriter(text))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.IndentChar = ' ';
                xmlWriter.Indentation = 2;

                var serializer = new XmlSerializer(typeof (ActivityElement));
                serializer.Serialize(xmlWriter, BuildTree(handle));

                return text.ToString();
            }
        }

        public static ActivityElement Decode(string output)
        {
            using (var text = new StringReader(output))
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

        static ActivityElement BuildTree(IActivityHandle handle)
        {
            var element = new ActivityElement();
            element.Name = handle.Name;
            element.Status = handle.Status;
            
            if (handle.Log != null)
            {
                element.Log = handle.Log.ToString();
            }

            if (handle.Error != null)
            {
                element.Error = handle.Error.ToString();
            }
            
            element.Children = handle.Children.AsParallel().Select(BuildTree).ToArray();
            
            return element;
        }
    }
}