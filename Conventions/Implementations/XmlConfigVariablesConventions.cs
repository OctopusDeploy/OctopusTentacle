using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;
using Octopus.Shared.Contracts;

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

        public void Install(IConventionContext context)
        {
            if (context.Variables.GetFlag(SpecialVariables.Action.Package.AutomaticallyUpdateAppSettingsAndConnectionStrings, true) == false)
            {
                return;
            }

            var configFiles = FileSystem.EnumerateFilesRecursively(context.PackageContentsDirectoryPath, "*.config");
            context.Log.Info("Looking for appSettings and connectionStrings in any .config files");

            foreach (var configFile in configFiles)
            {
                context.Log.VerboseFormat("Scanning configuration file: {0}", configFile);

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

        void UpdateConfigurationFile(string configurationFilePath, IConventionContext context)
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
                    ReplaceAttributeValues(doc, "//*[local-name()='appSettings']/*[local-name()='add']", "key", variable.Name, "value", variable.Value, context) |
                    ReplaceAttributeValues(doc, "//*[local-name()='connectionStrings']/*[local-name()='add']", "name", variable.Name, "connectionString", variable.Value, context) |
                    ReplaceAppSettingsValues(doc, "//*[local-name()='applicationSettings']//*[local-name()='setting']", "name", variable.Name, variable.Value, context);

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

        bool ReplaceAttributeValues(XDocument document, string xpath, string keyAttributeName, string keyAttributeValue, string valueAttributeName, string value, IConventionContext context)
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
                context.Log.VerboseFormat("Setting {0}[@{1}='{2}'] to '{3}'", xpath, keyAttributeName, keyAttributeValue, value);

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

        bool ReplaceAppSettingsValues(XDocument document, string xpath, string keyAttributeName, string keyAttributeValue, string value, IConventionContext context)
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
                context.Log.VerboseFormat("Setting {0}[@{1}='{2}'] to '{3}'", xpath, keyAttributeName, keyAttributeValue, value);

                var valueElement = setting.Elements().FirstOrDefault(e => e.Name.LocalName == "value");
                if (valueElement == null)
                {
                    setting.Add(new XElement("value", value));
                }
                else
                {
                    valueElement.SetValue(value);
                }
            }

            return modified;
        }
    }
}