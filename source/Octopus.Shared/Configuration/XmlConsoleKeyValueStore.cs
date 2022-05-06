using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class XmlConsoleKeyValueStore : FlatDictionaryKeyValueStore
    {
        readonly Action<string> writer;

        public XmlConsoleKeyValueStore()
            : this(Console.WriteLine)
        {
        }

        public XmlConsoleKeyValueStore(Action<string> writer)
            : base(JsonSerialization.GetDefaultSerializerSettings(), false, true)
        {
            this.writer = writer;
        }

        public override TData? Get<TData>(string name, TData? defaultValue, ProtectionLevel protectionLevel = ProtectionLevel.None) where TData : default
            => throw new NotSupportedException($"This store is a write-only store, because it is only intended for displaying formatted content to the console. Please use {nameof(XmlFileKeyValueStore)} if you need a readable store.");

        protected override void SaveSettings(IDictionary<string, object?> settingsToSave)
        {
            var settings = new XmlSettingsRoot();
            foreach (var key in settingsToSave.Keys.OrderBy(k => k))
                settings.Settings.Add(new XmlSetting { Key = key, Value = settingsToSave[key]?.ToString() });

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
                        writer(text);
                    }
                }
            }
        }

        protected override void LoadSettings(IDictionary<string, object?> settingsToFill)
        {
            throw new NotSupportedException($"This store is a write-only store, because it is only intended for displaying formatted content to the console. Please use {nameof(XmlFileKeyValueStore)} if you need a readable store.");
        }
    }
}