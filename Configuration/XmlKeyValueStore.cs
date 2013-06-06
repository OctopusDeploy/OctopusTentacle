using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Shared.Configuration
{
    public abstract class XmlKeyValueStore : DictionaryKeyValueStore
    {
        protected override void LoadSettings(IDictionary<string, string> settingsToFill)
        {
            if (!ExistsForReading())
            {
                return;
            }

            XmlSettingsRoot settings;
            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
            using (var xmlReader = new XmlTextReader(new StreamReader(OpenForReading())))
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
            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
            using (var xmlWriter = new XmlTextWriter(new StreamWriter(OpenForWriting())))
            {
                xmlWriter.Formatting = Formatting.Indented;

                var settings = new XmlSettingsRoot();
                foreach (var key in settingsToSave.Keys.OrderBy(k => k))
                {
                    settings.Settings.Add(new XmlSetting { Key = key, Value = settingsToSave[key] });
                }

                serializer.Serialize(xmlWriter, settings);
            }
        }

        protected abstract bool ExistsForReading();
        protected abstract Stream OpenForReading();
        protected abstract Stream OpenForWriting();
    }
}