using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public abstract class XmlKeyValueStore : DictionaryKeyValueStore
    {
        protected XmlKeyValueStore(bool autoSaveOnSet, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
        }

        protected override void LoadSettings(IDictionary<string, string> settingsToFill)
        {
            if (!ExistsForReading())
            {
                return;
            }

            XmlSettingsRoot settings;
            var serializer = new XmlSerializer(typeof (XmlSettingsRoot));
            using (var xmlReader = new XmlTextReader(new StreamReader(OpenForReading(), Encoding.UTF8)))
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
            var settings = new XmlSettingsRoot();
            foreach (var key in settingsToSave.Keys.OrderBy(k => k))
            {
                settings.Settings.Add(new XmlSetting { Key = key, Value = settingsToSave[key] });
            }

            var serializer = new XmlSerializer(typeof (XmlSettingsRoot));
            using (var stream = OpenForWriting())
            {
                stream.SetLength(0);
                using (var streamWriter = new StreamWriter(stream, Encoding.UTF8))
                {
                    using (var xmlWriter = new XmlTextWriter(streamWriter))
                    {
                        xmlWriter.Formatting = Formatting.Indented;

                        serializer.Serialize(xmlWriter, settings);
                    }
                }
            }
        }

        protected abstract bool ExistsForReading();
        protected abstract Stream OpenForReading();
        protected abstract Stream OpenForWriting();
    }
}