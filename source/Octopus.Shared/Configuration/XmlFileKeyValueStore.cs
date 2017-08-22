using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class XmlFileKeyValueStore : XmlKeyValueStore
    {
        readonly string configurationFile;

        public XmlFileKeyValueStore(string configurationFile, bool autoSaveOnSet = true, bool isWriteOnly = false) : base(autoSaveOnSet, isWriteOnly)
        {
            this.configurationFile = PathHelper.ResolveRelativeFilePath(configurationFile);
        }

        protected override void LoadSettings(IDictionary<string, object> settingsToFill)
        {
            if (!ExistsForReading())
                throw new Exception(string.Format("Configuration file {0} could not be found.", configurationFile));

            base.LoadSettings(settingsToFill);
        }

        protected override bool ExistsForReading()
        {
            return File.Exists(configurationFile);
        }

        protected override Stream OpenForReading()
        {
            return new FileStream(configurationFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        protected override Stream OpenForWriting()
        {
            return new FileStream(configurationFile, FileMode.OpenOrCreate, FileAccess.Write);
        }
    }
}