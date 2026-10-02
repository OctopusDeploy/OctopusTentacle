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
        /// <summary>
        /// Appended to the configuration file's name for the copy kept before the first re-encryption.
        /// </summary>
        public const string PreReEncryptionBackupSuffix = ".before-reencryption";

        static readonly ISystemLog Log = new SystemLog();

        readonly IOctopusFileSystem fileSystem;
        readonly string configurationFile;
        bool backedUpBeforeReEncrypting;

        /// <param name="encryptor">Protects <see cref="ProtectionLevel.MachineKey"/> values. Null means the encryptor for this configuration file, <see cref="MachineKeyEncryptor.ForConfigurationFile"/>; tests pass their own.</param>
        public XmlFileKeyValueStore(IOctopusFileSystem fileSystem,
            string configurationFile,
            bool autoSaveOnSet = true,
            bool isWriteOnly = false,
            IMachineKeyEncryptor? encryptor = null) : base(encryptor ?? MachineKeyEncryptor.ForConfigurationFile(fileSystem.GetFullPath(configurationFile)), autoSaveOnSet, isWriteOnly)
        {
            this.fileSystem = fileSystem;
            this.configurationFile = fileSystem.GetFullPath(configurationFile);
        }

        public string PreReEncryptionBackupFile => configurationFile + PreReEncryptionBackupSuffix;

        /// <summary>
        /// Before the first value is rewritten, keeps a copy of the file exactly as the earlier version left it, so that
        /// version can be reinstalled and pointed at it. The copy is readable only by its owner and is never replaced,
        /// so it stays the oldest copy. If it cannot be written the re-encryption does not happen, because a downgrade
        /// would then have nothing to go back to.
        /// </summary>
        protected override void BeforeReEncrypting()
        {
            if (backedUpBeforeReEncrypting)
                return;

            var backup = PreReEncryptionBackupFile;
            if (!fileSystem.FileExists(backup) && fileSystem.TryCreateFileOwnerOnly(backup, ReadConfigurationFileBytes()))
            {
                Log.Info($"Saved a copy of `{configurationFile}` as it was before re-encryption to `{backup}`. "
                    + "An earlier version of Tentacle can read that copy if you need to downgrade; delete it once you no longer need to.");
            }

            backedUpBeforeReEncrypting = true;
        }

        /// <summary>
        /// Tightens the key file (via the encryptor) and the configuration file itself. The configuration was usually
        /// created 0644; nothing in it is meant for other local users, and the key beside it is already owner-only, so
        /// leaving the file it protects world-readable would be odd. Only the agent calls this, as the service user, so
        /// a file it owns can never be tightened away from the process that reads it. Failures are logged and ignored.
        /// </summary>
        public override void RestrictKeyStorageToOwner()
        {
            base.RestrictKeyStorageToOwner();
            try
            {
                if (fileSystem.FileExists(configurationFile) && fileSystem.RestrictFilePermissionsToOwner(configurationFile))
                    Log.Info($"Restricted the permissions on `{configurationFile}` so that only its owner can read it.");
            }
            catch (Exception e)
            {
                Log.Verbose(e, $"Unable to restrict the permissions on `{configurationFile}`. It is still usable.");
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
            return new FileStream(configurationFile, FileMode.OpenOrCreate, FileAccess.Write);
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