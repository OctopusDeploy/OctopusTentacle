using System;
using Octopus.Platform.Deployment.Conventions;
using Octopus.Platform.Variables;
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

        public void Install(IConventionContext context)
        {
            if (!context.Variables.GetFlag(SpecialVariables.Action.IsFtpDeployment, false))
                return;

            var host = context.Variables.Get(SpecialVariables.Action.Ftp.Host);
            var username = context.Variables.Get(SpecialVariables.Action.Ftp.Username);
            var password = context.Variables.Get(SpecialVariables.Action.Ftp.Password);
            var useFtps = context.Variables.GetFlag(SpecialVariables.Action.Ftp.UseFtps, false);
            var root = context.Variables.Get(SpecialVariables.Action.Ftp.RootDirectory);
            var port = context.Variables.GetInt32(SpecialVariables.Action.Ftp.FtpPort) ?? 0;
            var deleteFiles = context.Variables.GetFlag(SpecialVariables.Action.Ftp.DeleteDestinationFiles, false);

            if (host.Contains("/") || host.Contains(":"))
            {
                Uri uri;
                if (Uri.TryCreate(host, UriKind.Absolute, out uri))
                {
                    host = uri.Host;
                }
            }

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