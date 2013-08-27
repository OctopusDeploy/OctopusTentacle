using System;
using System.Collections.Generic;
using System.IO;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;

namespace Octopus.Shared.Configuration
{
    public class XmlFileKeyValueStore : XmlKeyValueStore
    {
        readonly ILog log;
        readonly string configurationFile;

        public XmlFileKeyValueStore(string configurationFile, ILog log)
        {
            this.configurationFile = PathHelper.ResolveRelativeFilePath(configurationFile);
            this.log = log;
        }

        protected override void LoadSettings(IDictionary<string, string> settingsToFill)
        {
            log.InfoFormat("Loading configuration settings from file {0}", configurationFile);
            if (!ExistsForReading())
                throw new Exception(string.Format("Configuration file {0} could not be found.", configurationFile));

            base.LoadSettings(settingsToFill);
        }

        protected override void SaveSettings(IDictionary<string, string> settingsToSave)
        {
            log.InfoFormat("Saving configuration settings to file {0}", configurationFile);
            base.SaveSettings(settingsToSave);
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