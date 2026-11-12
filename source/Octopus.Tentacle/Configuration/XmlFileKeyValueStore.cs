using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration
{
    public class XmlFileKeyValueStore : XmlKeyValueStore
    {
        static readonly ISystemLog Log = new SystemLog();

        readonly IOctopusFileSystem fileSystem;
        readonly string configurationFile;

        public XmlFileKeyValueStore(IOctopusFileSystem fileSystem,
            string configurationFile,
            bool autoSaveOnSet = true,
            bool isWriteOnly = false,
            IMachineKeyEncryptor? encryptor = null) : base(encryptor ?? MachineKeyEncryptor.ForConfigurationFile(fileSystem, fileSystem.GetFullPath(configurationFile)), autoSaveOnSet, isWriteOnly)
        {
            this.fileSystem = fileSystem;
            this.configurationFile = fileSystem.GetFullPath(configurationFile);
        }

        public override void RestrictKeyStorageToOwner()
        {
            base.RestrictKeyStorageToOwner();
            try
            {
                if (fileSystem.FileExists(configurationFile) && fileSystem.RestrictFilePermissionsToOwner(configurationFile))
                {
                    Log.Info($"Restricted the permissions on `{configurationFile}` so that only its owner can read it.");
                }
            }
            catch (Exception e)
            {
                Log.Warn(e, $"Unable to restrict the permissions on `{configurationFile}`. It is still usable.");
            }
        }

        protected override void LoadSettings(IDictionary<string, object?> settingsToFill)
        {
            if (!ExistsForReading())
            {
                throw new Exception($"Configuration file {configurationFile} could not be found.");
            }

            base.LoadSettings(settingsToFill);
        }

        protected override bool ExistsForReading()
            => File.Exists(configurationFile);

        protected override Stream OpenForReading()
            => new FileStream(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        protected override Stream OpenForWriting()
        {
            fileSystem.EnsureDiskHasEnoughFreeSpace(configurationFile, 1024 * 1024);
            return new WrittenOnDisposeStream(contents => fileSystem.ReplaceFile(configurationFile, contents));
        }

        class WrittenOnDisposeStream : MemoryStream
        {
            readonly Action<byte[]> write;
            bool written;

            public WrittenOnDisposeStream(Action<byte[]> write)
            {
                this.write = write;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !written)
                {
                    written = true;
                    write(ToArray());
                }
                base.Dispose(disposing);
            }
        }

        protected override bool ValueNeedsToBeSerialized(ProtectionLevel protectionLevel, object valueAsObject)
        {
            //historically, we wrote bools as lowercase (ie, json style), not Title Case (ie, .net style)
            //we can now read case insensitive, but historically we couldn't
            //so, if a customer rolls back to an older version, using a modern config file
            //it will fail - lets write as json to the xml file only.
            if (valueAsObject is bool)
                return true;
            return base.ValueNeedsToBeSerialized(protectionLevel, valueAsObject);
        }
    }
}