﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Octopus.Platform.Util;
using Octopus.Platform.Variables;
using Octopus.Shared.Activities;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Azure;

namespace Octopus.Shared.Conventions.Implementations
{
    public class AzureConfigurationConvention : IInstallationConvention
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IAzureConfigurationRetriever configurationRetriever;

        public AzureConfigurationConvention(IOctopusFileSystem fileSystem, IAzureConfigurationRetriever configurationRetriever)
        {
            this.fileSystem = fileSystem;
            this.configurationRetriever = configurationRetriever;
        }

        public int Priority { get { return ConventionPriority.AzureConfiguration; } }
        public string FriendlyName { get { return "Azure Configuration"; } }

        public void Install(IConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsAzureDeployment, false))
                return;

            var configurationFilePath = ChooseWhichServiceConfigurationFileToUse(context);
            var configurationFile = LoadConfigurationFile(configurationFilePath);

            UpdateConfigurationBasedOnCurrentInstanceCount(configurationFile, configurationFilePath, context);
            UpdateConfigurationSettings(configurationFile, context);

            SaveConfigurationFile(configurationFile, configurationFilePath);
        }

        void UpdateConfigurationSettings(XContainer configurationFile, IConventionContext context)
        {
            context.Log.Verbose("Updating configuration settings...");

            var variables = context.Variables.AsDictionary();

            WithConfigurationSettings(configurationFile, (roleName, settingName, settingValueAttribute) =>
            {
                string value;
                if (variables.TryGetValue(roleName + "/" + settingName, out value)
                    || variables.TryGetValue(roleName + "\\" + settingName, out value)
                    || variables.TryGetValue(settingName, out value))
                {
                    context.Log.Verbose("  Updating setting for role " + roleName + ": " + settingName + " = " + value);
                    settingValueAttribute.Value = value;
                }
            });
        }

        string ChooseWhichServiceConfigurationFileToUse(IConventionContext context)
        {
            var configurationFilePath = context.Variables.Get("OctopusAzureConfigurationFile");
            if (!string.IsNullOrWhiteSpace(configurationFilePath) && !fileSystem.FileExists(configurationFilePath))
            {
                throw new ActivityFailedException("The specified Azure service configuraton file does not exist: " + configurationFilePath);
            }

            if (string.IsNullOrWhiteSpace(configurationFilePath))
            {
                var userSpecifiedFile = context.Variables.Get("OctopusAzureConfigurationFileName");

                configurationFilePath = GetFirstExistingFile(
                    context,
                    userSpecifiedFile,
                    "ServiceConfiguration." + context.Variables.Get(SpecialVariables.Environment.Name) + ".cscfg",
                    "ServiceConfiguration.Cloud.cscfg");
            }

            context.Variables.Set("OctopusAzureConfigurationFile", configurationFilePath);

            return configurationFilePath;
        }

        string GetFirstExistingFile(IConventionContext context, params string[] fileNames)
        {
            foreach (var name in fileNames)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var path = Path.Combine(context.PackageContentsDirectoryPath, name);
                if (fileSystem.FileExists(path))
                {
                    context.Log.Verbose("Found Azure service configuration file: " + path);
                    return path;
                }
                
                context.Log.Verbose("Azure service configuration file not found: " + path);
            }

            throw new ActivityFailedException("Could not find an Azure service configuration file in the package.");
        }

        static XDocument LoadConfigurationFile(string configurationFilePath)
        {
            using (var reader = XmlReader.Create(configurationFilePath))
            {
                return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
        }

        void UpdateConfigurationBasedOnCurrentInstanceCount(XContainer localConfigurationFile, string configurationFileName, IConventionContext context)
        {
            var useInstanceCount = context.Variables.GetFlag(SpecialVariables.Step.Azure.UseCurrentInstanceCount, false);
            if (useInstanceCount == false)
                return;

            var serviceName = context.Variables.Get(SpecialVariables.Step.Azure.CloudServiceName);
            var slot = context.Variables.Get(SpecialVariables.Step.Azure.Slot);

            var subscriptionData = SubscriptionDataFactory.CreateFromAzureStep(context.Variables, context.Certificate);
            var remoteConfigurationFile = configurationRetriever.GetCurrentConfiguration(subscriptionData, serviceName, slot);

            if (remoteConfigurationFile == null)
            {
                context.Log.Info("There is no current deployment of service '{0}' in slot '{1}', so existing instance counts will not be imported.");
                return;
            }

            var rolesByCount = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            context.Log.Verbose("Local instance counts (from " + Path.GetFileName(configurationFileName) + "): ");
            WithInstanceCounts(localConfigurationFile, (roleName, attribute) =>
            {
                context.Log.Verbose(" - " + roleName + " = " + attribute.Value);

                string value;
                if (rolesByCount.TryGetValue(roleName, out value))
                {
                    attribute.SetValue(value);
                }
            });

            context.Log.Verbose("Remote instance counts: ");
            WithInstanceCounts(remoteConfigurationFile, (roleName, attribute) =>
            {
                rolesByCount[roleName] = attribute.Value;
                context.Log.Verbose(" - " + roleName + " = " + attribute.Value);
            });

            context.Log.Verbose("Replacing local instance count settings with remote settings: ");
            WithInstanceCounts(localConfigurationFile, (roleName, attribute) =>
            {
                string value;
                if (!rolesByCount.TryGetValue(roleName, out value)) 
                    return;

                attribute.SetValue(value);
                context.Log.Verbose(" - " + roleName + " = " + attribute.Value);
            });
        }

        static void WithInstanceCounts(XContainer configuration, Action<string, XAttribute> roleAndCountAttributeCallback)
        {
            foreach (var roleElement in configuration.Elements()
                .SelectMany(e => e.Elements())
                .Where(e => e.Name.LocalName == "Role"))
            {
                var roleNameAttribute = roleElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "name");
                if (roleNameAttribute == null)
                    continue;

                var instancesElement = roleElement.Elements().FirstOrDefault(e => e.Name.LocalName == "Instances");
                if (instancesElement == null)
                    continue;

                var countAttribute = instancesElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "count");
                if (countAttribute == null)
                    continue;

                roleAndCountAttributeCallback(roleNameAttribute.Value, countAttribute);
            }
        }

        static void WithConfigurationSettings(XContainer configuration, Action<string, string, XAttribute> roleSettingNameAndValueAttributeCallback)
        {
            foreach (var roleElement in configuration.Elements()
                .SelectMany(e => e.Elements())
                .Where(e => e.Name.LocalName == "Role"))
            {
                var roleNameAttribute = roleElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "name");
                if (roleNameAttribute == null)
                    continue;

                var configSettingsElement = roleElement.Elements().FirstOrDefault(e => e.Name.LocalName == "ConfigurationSettings");
                if (configSettingsElement == null)
                    continue;

                foreach (var settingElement in configSettingsElement.Elements().Where(e => e.Name.LocalName == "Setting"))
                {
                    var nameAttribute = settingElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "name");
                    if (nameAttribute == null)
                        continue;

                    var valueAttribute = settingElement.Attributes().FirstOrDefault(x => x.Name.LocalName == "value");
                    if (valueAttribute == null)
                        continue;

                    roleSettingNameAndValueAttributeCallback(roleNameAttribute.Value, nameAttribute.Value, valueAttribute);
                }
            }
        }

        static void SaveConfigurationFile(XDocument document, string configurationFilePath)
        {
            var xws = new XmlWriterSettings { OmitXmlDeclaration = document.Declaration == null };
            using (var writer = XmlWriter.Create(configurationFilePath, xws))
            {
                document.Save(writer);
            }
        }
    }
}