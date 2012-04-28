using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Shared.Activities
{
    public class ActivityEncoder
    {
        public static string RenderLog(IActivityState state)
        {
            using (var text = new StringWriter())
            using (var xmlWriter = new XmlTextWriter(text))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.IndentChar = ' ';
                xmlWriter.Indentation = 2;

                var serializer = new XmlSerializer(typeof (ActivityElement));
                serializer.Serialize(xmlWriter, BuildTree(state));

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

        static ActivityElement BuildTree(IActivityState state)
        {
            var element = new ActivityElement();
            element.Name = state.Name;
            element.Status = state.Status;
            
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