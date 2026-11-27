using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NSubstitute;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;
using Formatting = System.Xml.Formatting;

namespace Octopus.Tentacle.Tests.Support
{
    enum KeyValueStoreSource
    {
        InMemory,
        ReloadedFromFile,
        WrittenByOldImplementation
    }

    class KeyValueStoreBuilder
    {
        readonly List<Action<ISettingsWriter>> writes = new();
        KeyValueStoreSource source = KeyValueStoreSource.InMemory;

        public KeyValueStoreBuilder From(KeyValueStoreSource source)
        {
            this.source = source;
            return this;
        }

        public KeyValueStoreBuilder With<TData>(string key, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            writes.Add(writer => writer.Set(key, value, protectionLevel));
            return this;
        }

        public IKeyValueStore Build()
        {
            var configurationFile = Path.GetTempFileName();
            var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");

            ISettingsWriter writer = source == KeyValueStoreSource.WrittenByOldImplementation
                ? new OldImplementation(configurationFile)
                : new XmlFileKeyValueStoreWriter(new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: TestMachineKeyEncryptor.Current));

            foreach (var write in writes)
                write(writer);
            writer.Save();

            return source == KeyValueStoreSource.InMemory
                ? ((XmlFileKeyValueStoreWriter)writer).Store
                : new XmlFileKeyValueStore(fileSystem, configurationFile, encryptor: TestMachineKeyEncryptor.Current);
        }

        interface ISettingsWriter
        {
            void Set<TData>(string name, TData value, ProtectionLevel protectionLevel);
            void Save();
        }

        class XmlFileKeyValueStoreWriter : ISettingsWriter
        {
            public XmlFileKeyValueStoreWriter(XmlFileKeyValueStore store) => Store = store;

            public XmlFileKeyValueStore Store { get; }

            public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel) => Store.Set(name, value, protectionLevel);

            public void Save() => Store.Save();
        }

        /// <summary>
        /// A rough copy of the implementation from around https://github.com/OctopusDeploy/OctopusShared/tree/fac3c1dbdb51d3cf71ec5513f2c24d76b18568ee
        /// </summary>
        class OldImplementation : ISettingsWriter
        {
            readonly string configurationFile;
            readonly IDictionary<string, object?> settings = new Dictionary<string, object?>();

            public OldImplementation(string configurationFile)
            {
                this.configurationFile = configurationFile;
            }

            public void Set<TData>(string name, TData value, ProtectionLevel protectionLevel)
            {
                var serialized = typeof(TData) == typeof(string)
                    ? (string?)(object?)value
                    : JsonConvert.SerializeObject(value);

                if (string.IsNullOrWhiteSpace(serialized))
                {
                    settings[name] = null;
                    return;
                }

                settings[name] = protectionLevel == ProtectionLevel.MachineKey
                    ? TestMachineKeyEncryptor.Current.Encrypt(serialized)
                    : serialized;
            }

            public void Save()
            {
                var root = new XmlSettingsRoot();
                foreach (var key in settings.Keys.OrderBy(k => k))
                    root.Settings.Add(new XmlSetting { Key = key, Value = settings[key]?.ToString() });

                var serializer = new XmlSerializer(typeof(XmlSettingsRoot));
                using var stream = new FileStream(configurationFile, FileMode.OpenOrCreate, FileAccess.Write);
                stream.SetLength(0);
                using var streamWriter = new StreamWriter(stream, Encoding.UTF8);
                using var xmlWriter = new XmlTextWriter(streamWriter);
                xmlWriter.Formatting = Formatting.Indented;
                serializer.Serialize(xmlWriter, root);
            }
        }
    }
}
