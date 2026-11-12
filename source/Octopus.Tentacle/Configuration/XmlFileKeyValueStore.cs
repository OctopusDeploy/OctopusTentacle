using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration
{
    public class XmlFileKeyValueStore : XmlKeyValueStore
    {
        public const string PreReEncryptionBackupSuffix = ".before-reencryption";

        static readonly ISystemLog Log = new SystemLog();

        readonly IOctopusFileSystem fileSystem;
        readonly string configurationFile;
        bool backedUpBeforeReEncrypting;

        public XmlFileKeyValueStore(IOctopusFileSystem fileSystem,
            string configurationFile,
            bool autoSaveOnSet = true,
            bool isWriteOnly = false,
            IMachineKeyEncryptor? encryptor = null) : base(encryptor ?? MachineKeyEncryptor.ForConfigurationFile(fileSystem, fileSystem.GetFullPath(configurationFile)), autoSaveOnSet, isWriteOnly)
        {
            this.fileSystem = fileSystem;
            this.configurationFile = fileSystem.GetFullPath(configurationFile);
        }

        string PreReEncryptionBackupFile => configurationFile + PreReEncryptionBackupSuffix;

        // A copy of the configuration as an earlier version wrote it, so it can still be used after a downgrade. It is only
        // replaced by a configuration that holds no current-format values (after a downgrade and another upgrade, say), so
        // a configuration that was only partly re-encrypted never overwrites the copy taken before that started.
        protected override void BeforeReEncrypting()
        {
            if (backedUpBeforeReEncrypting)
            {
                return;
            }

            var backup = PreReEncryptionBackupFile;
            var current = ReadConfigurationFileBytes();
            if (fileSystem.FileExists(backup))
            {
                if (!WrittenByAnEarlierVersion(current))
                {
                    backedUpBeforeReEncrypting = true;
                    return;
                }
                fileSystem.DeleteFile(backup);
            }

            if (fileSystem.TryCreateFileOwnerOnly(backup, current))
            {
                Log.Info($"Saved a copy of `{configurationFile}` as it was before re-encryption to `{backup}`. "
                    + "An earlier version of Tentacle can read that copy if you need to downgrade; delete it once you no longer need to.");
            }

            backedUpBeforeReEncrypting = true;
        }

        static bool WrittenByAnEarlierVersion(byte[] configuration)
            => !Encoding.UTF8.GetString(configuration).Contains(LinuxMachineKeyEncryptor.ProtectedValuePrefix);

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

        byte[] ReadConfigurationFileBytes()
        {
            using var stream = OpenForReading();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
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