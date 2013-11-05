using System;
using System.IO;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Security.MasterKey;
using Octopus.Platform.Util;
using Octopus.Shared.Configuration;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Communications.Commands
{
    public class DecryptCommand : AbstractStandardCommand
    {
        readonly IOctopusFileSystem fileSystem;
        readonly Lazy<ICommunicationsConfiguration> communications;
        readonly Lazy<IMasterKeyEncryption> encryption;

        public DecryptCommand(
            IOctopusFileSystem fileSystem,
            Lazy<ICommunicationsConfiguration> communications,
            Lazy<IMasterKeyEncryption> encryption,
            IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.fileSystem = fileSystem;
            this.communications = communications;
            this.encryption = encryption;
        }

        protected override void Start()
        {
            base.Start();

            var log = Log.Octopus();

            log.Info("Decrypting communication state.");
            log.Warn("Sensitive data may be visible in the resulting .clear files!");

            int decryptedMessages, messageErrors, decryptedActors, actorErrors;
            DecryptFiles(communications.Value.MessagesDirectory, new[] { "*.pf", "*.pf.read" }, log, out decryptedMessages, out messageErrors);
            DecryptFiles(communications.Value.ActorStateDirectory, new[] { "*.pfa" }, log, out decryptedActors, out actorErrors);

            log.InfoFormat("Decrypted {0} messages ({1} failed), {2} actors ({3} failed)", decryptedMessages, messageErrors, decryptedActors, actorErrors);
            if (actorErrors > 0 || messageErrors > 0)
                log.Warn("One or more errors were encountered.");
        }

        void DecryptFiles(string directory, string[] searchPatterns, ILog log, out int purgedCount,  out int errorCount)
        {
            log.InfoFormat("Decrypting data in \"{0}\"...", directory);

            purgedCount = 0;
            errorCount = 0;

            foreach (var filename in fileSystem.EnumerateFilesRecursively(directory, searchPatterns))
            {
                try
                {
                    var clearFilename = filename + "-" + Guid.NewGuid().ToString("N") + ".clear";
                    using (var clear = fileSystem.OpenFile(clearFilename, FileMode.CreateNew))
                    using (var cipher = fileSystem.OpenFile(filename, FileMode.Open))
                    using (var transform = encryption.Value.ReadAsPlaintext(cipher))
                    {
                        transform.CopyTo(clear);
                    }

                    ++purgedCount;
                    log.VerboseFormat("Decypted \"{0}\"", filename);
                }
                catch (Exception ex)
                {
                    ++errorCount;
                    log.ErrorFormat(ex, "Failed to decrypt \"{0}\"", filename);
                }
            }
        }
    }
}
