using System;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Communications.Commands
{
    public class ResetCommand : AbstractStandardCommand
    {
        readonly IOctopusFileSystem fileSystem;
        readonly Lazy<ICommunicationsConfiguration> communications;
        bool soft;

        public ResetCommand(IOctopusFileSystem fileSystem, Lazy<ICommunicationsConfiguration> communications, IApplicationInstanceSelector selector)
            : base(selector)
        {
            this.fileSystem = fileSystem;
            this.communications = communications;

            Options.Add("soft", "Just rename files rather than deleting them", v => soft = true);
        }

        protected override void Start()
        {
            base.Start();

            var log = Log.Octopus();

            log.Info("Resetting stored communication state");

            int purgedMessages, messageErrors, purgedActors, actorErrors;
            PurgeFiles(communications.Value.MessagesDirectory, new[] { "*.pf", "*.pf.read" }, log, out purgedMessages, out messageErrors);
            PurgeFiles(communications.Value.ActorStateDirectory, new[] { "*.pfa" }, log, out purgedActors, out actorErrors);

            log.InfoFormat("Purged {0} messages ({1} failed), {2} actors ({3} failed)", purgedMessages, messageErrors, purgedActors, actorErrors);
            if (actorErrors > 0 || messageErrors > 0)
                log.Warn("One or more errors were encountered. Try using the 'service' command to stop services before resetting again.");
        }

        void PurgeFiles(string directory, string[] searchPatterns, ILog log, out int purgedCount,  out int errorCount)
        {
            log.InfoFormat("Purging data from \"{0}\"...", directory);

            purgedCount = 0;
            errorCount = 0;

            foreach (var filename in fileSystem.EnumerateFilesRecursively(directory, searchPatterns))
            {
                try
                {
                    if (soft)
                    {
                        var purgedFilename = filename + "-" + Guid.NewGuid().ToString("N") + ".purged";
                        fileSystem.MoveFile(filename, purgedFilename);
                    }
                    else
                    {
                        fileSystem.DeleteFile(filename);
                    }
                    ++purgedCount;
                    log.VerboseFormat("Purged \"{0}\"", filename);
                }
                catch (Exception ex)
                {
                    ++errorCount;
                    log.ErrorFormat(ex, "Failed to purge \"{0}\"", filename);
                }
            }
        }
    }
}
