using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Octopus.Shared.Configuration
{
    public class XmlConsoleKeyValueStore : DictionaryKeyValueStore
    {
        public XmlConsoleKeyValueStore() : base(autoSaveOnSet:false, isWriteOnly:true)
        {
        }

        protected override void SaveSettings(IDictionary<string, string> settingsToSave)
        {
            var settings = new XmlSettingsRoot();
            foreach (var key in settingsToSave.Keys.OrderBy(k => k))
            {
                settings.Settings.Add(new XmlSetting { Key = key, Value = settingsToSave[key] });
            }

            var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
            using (var stream = new MemoryStream())
            {
                stream.SetLength(0);
                using (var xmlWriter = new XmlTextWriter(new StreamWriter(stream, Encoding.UTF8)))
                {
                    xmlWriter.Formatting = Formatting.Indented;

                    serializer.Serialize(xmlWriter, settings);

                    stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(stream))
                    {
                        var text = reader.ReadToEnd();
                        Console.WriteLine(text);
                    }
                }
            }
        }
    }
}