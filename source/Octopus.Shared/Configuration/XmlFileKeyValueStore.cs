using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class XmlFileKeyValueStore : XmlKeyValueStore
    {
        readonly IOctopusFileSystem fileSystem;
        readonly string configurationFile;

        public XmlFileKeyValueStore(IOctopusFileSystem fileSystem,
            string configurationFile,
            bool autoSaveOnSet = true,
            bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
            this.fileSystem = fileSystem;
            this.configurationFile = PathHelper.ResolveRelativeWorkingDirectoryFilePath(configurationFile);
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