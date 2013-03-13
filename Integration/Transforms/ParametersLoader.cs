using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace Octopus.Shared.Integration.Transforms
{
    public class ParametersLoader
    {
        public static void LoadParameters(string parametersFile, IDictionary<string, string> parameters)
        {
            if (string.IsNullOrWhiteSpace(parametersFile))
                throw new ArgumentException("parametersFile");

            if (!File.Exists(parametersFile))
                throw new FileNotFoundException(string.Format("Couldn't find file with parameters '{0}'.", parametersFile));

            var document = XDocument.Load(parametersFile);
            var root = document.Element("parameters");

            if (root == null)
                throw new FormatException("Couldn't find root element 'parameters'.");

            foreach (var element in root.Elements())
            {
                if (element.Name != "param")
                    throw new FormatException("All parameters elements should be 'param'.");

                var attrName = element.Attribute("name");

                if (attrName == null)
                    throw new FormatException("Attribute 'name' is required for 'param' element.");

                var attrValue = element.Attribute("value");

                if (attrValue == null)
                    throw new FormatException("Attribute 'value' is required for 'param' element.");

                parameters.Add(attrName.Value, attrValue.Value);
            }
        }
    }
}