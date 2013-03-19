using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Octopus.Shared.Contracts;
using Octopus.Shared.Util;

namespace Octopus.Shared.Conventions.Implementations
{
    public class XmlConfigVariablesConventions : IInstallationConvention 
    {
        public IOctopusFileSystem FileSystem { get; set; }

        public int Priority
        {
            get { return ConventionPriority.ConfigVariables; }
        }

        public string FriendlyName { get { return "XML Configuration"; } }

        public void Install(ConventionContext context)
        {
            if (context.Variables.GetFlag(SpecialVariables.Step.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings, true) == false)
            {
                return;
            }

            var configFiles = FileSystem.EnumerateFilesRecursively(context.PackageContentsDirectoryPath, "*.config");
            context.Log.Info("Looking for appSettings and connectionStrings in any .config files");

            foreach (var configFile in configFiles)
            {
                context.Log.DebugFormat("Scanning configuration file: {0}", configFile);

                try
                {
                    UpdateConfigurationFile(configFile, context);
                }
                catch (Exception ex)
                {
                    var warnAsErrors = context.Variables.GetFlag(SpecialVariables.TreatWarningsAsErrors, false);
                    if (warnAsErrors)
                    {
                        throw;
                    }

                    context.Log.Warn("Unable to update the config file - it may not be a valid XML file: " + ex.Message);
                }
            }
        }

        void UpdateConfigurationFile(string configurationFilePath, ConventionContext context)
        {
            var variables = context.Variables;

            XDocument doc;

            using (var reader = XmlReader.Create(configurationFilePath))
            {
                doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
            
            var modified = false;

            foreach (var variable in variables.AsList())
            {
                var changed = 
                    ReplaceAttributeValues(doc, "//*[local-name()='appSettings']/*[local-name()='add']", "key", variable.Name, "value", variable.Value, context) ||
                    ReplaceAttributeValues(doc, "//*[local-name()='connectionStrings']/*[local-name()='add']", "name", variable.Name, "connectionString", variable.Value, context);

                if (changed) modified = true;
            }

            if (!modified)
                return;

            var xws = new XmlWriterSettings {OmitXmlDeclaration = doc.Declaration == null, Indent = true};
            using (var writer = XmlWriter.Create(configurationFilePath, xws))
            {
                doc.Save(writer);
            }
        }

        bool ReplaceAttributeValues(XDocument document, string xpath, string keyAttributeName, string keyAttributeValue, string valueAttributeName, string value, ConventionContext context)
        {
            var settings =
                from element in document.XPathSelectElements(xpath)
                let keyAttribute = element.Attribute(keyAttributeName)
                where keyAttribute != null
                where string.Equals(keyAttribute.Value, keyAttributeValue, StringComparison.InvariantCultureIgnoreCase)
                select element;

            value = value ?? string.Empty;

            var modified = false;

            foreach (var setting in settings)
            {
                modified = true;
                context.Log.DebugFormat("Setting {0}[@{1}='{2}'] to '{3}'", xpath, keyAttributeName, keyAttributeValue, value);

                var valueAttribute = setting.Attribute(valueAttributeName);
                if (valueAttribute == null)
                {
                    setting.Add(new XAttribute(valueAttributeName, value));
                }
                else
                {
                    valueAttribute.SetValue(value);
                }
            }

            return modified;
        }
    }
}