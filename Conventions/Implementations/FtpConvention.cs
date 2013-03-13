using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Integration.Ftp;

namespace Octopus.Shared.Conventions.Implementations
{
    public class FtpConvention : IInstallationConvention
    {
        readonly IFtpSynchronizer synchronizer;

        public FtpConvention(IFtpSynchronizer synchronizer)
        {
            this.synchronizer = synchronizer;
        }

        public int Priority { get { return ConventionPriority.Ftp; } }

        public string FriendlyName { get { return "FTP"; } }

        public void Install(ConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Step.IsFtpDeployment, false))
                return;

            var host = context.Variables.GetValue(SpecialVariables.Step.Ftp.Host);
            var username = context.Variables.GetValue(SpecialVariables.Step.Ftp.Username);
            var password = context.Variables.GetValue(SpecialVariables.Step.Ftp.Password);
            var useFtps = context.Variables.GetFlag(SpecialVariables.Step.Ftp.UseFtps, false);
            var root = context.Variables.GetValue(SpecialVariables.Step.Ftp.RootDirectory);
            var port = context.Variables.GetInt32(SpecialVariables.Step.Ftp.FtpPort) ?? 0;
            var deleteFiles = context.Variables.GetFlag(SpecialVariables.Step.Ftp.DeleteDestinationFiles, false);

            context.Log.Info("Begin synchronization...");

            var settings = new FtpSynchronizationSettings(host, username, password, useFtps, context.Log, context.CancellationToken);
            settings.LocalDirectory = context.PackageContentsDirectoryPath;
            settings.RemoteDirectory = root;
            settings.Port = port;
            settings.DeleteDestinationFiles = deleteFiles;
            
            synchronizer.Synchronize(settings);
        }
    }
}