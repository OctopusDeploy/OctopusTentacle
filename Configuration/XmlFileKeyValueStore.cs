using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class XmlFileKeyValueStore : DictionaryKeyValueStore
    {
        readonly ILog log;
        readonly string configurationFile;

        public XmlFileKeyValueStore(string configurationFile, ILog log)
        {
            this.configurationFile = PathHelper.ResolveRelativeFilePath(configurationFile);
            this.log = log;
        }

        protected override void LoadSettings(IDictionary<string, string> settingsToFill)
        {
            log.InfoFormat("Loading configuration settings from file {0}", configurationFile);

            XmlSettingsRoot settings;
            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
            using (var xmlReader = new XmlTextReader(new StreamReader(new FileStream(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read))))
            {
                settings = (XmlSettingsRoot)serializer.Deserialize(xmlReader);
            }

            foreach (var setting in settings.Settings)
            {
                settingsToFill[setting.Key] = setting.Value;
            }
        }

        protected override void SaveSettings(IDictionary<string, string> settingsToSave)
        {
            log.InfoFormat("Saving configuration settings to file {0}", configurationFile);

            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
            using (var xmlReader = new XmlTextWriter(new StreamWriter(new FileStream(configurationFile, FileMode.OpenOrCreate, FileAccess.Write))))
            {
                var settings = new XmlSettingsRoot();
                foreach (var key in settingsToSave.Keys.OrderBy(k => k))
                {
                    settings.Settings.Add(new XmlSetting { Key = key, Value = settingsToSave[key] });
                }

                serializer.Serialize(xmlReader, settings);
            }
        }
    }
}