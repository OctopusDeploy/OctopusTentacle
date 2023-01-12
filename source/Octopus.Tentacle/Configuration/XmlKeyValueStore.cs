using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Tentacle.Configuration
{
    public abstract class XmlKeyValueStore : FlatDictionaryKeyValueStore
    {
        protected XmlKeyValueStore(bool autoSaveOnSet, bool isWriteOnly = false) : base(JsonSerialization.GetDefaultSerializerSettings(), autoSaveOnSet, isWriteOnly)
        {
        }

        protected override void LoadSettings(IDictionary<string, object?> settingsToFill)
        {
            if (!ExistsForReading())
                return;

            XmlSettingsRoot settings;
            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
            using (var xmlReader = new XmlTextReader(new StreamReader(OpenForReading(), Encoding.UTF8)))
            {
                var obj = serializer.Deserialize(xmlReader);
                if (obj is null)
                {
                    return;
                }
                
                settings = (XmlSettingsRoot) obj;
            }

            foreach (var setting in settings.Settings)
                settingsToFill[setting.Key] = setting.Value;
        }

        protected override void SaveSettings(IDictionary<string, object?> settingsToSave)
        {
            var settings = new XmlSettingsRoot();
            foreach (var key in settingsToSave.Keys.OrderBy(k => k))
                settings.Settings.Add(new XmlSetting { Key = key, Value = settingsToSave[key]?.ToString() });

            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
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