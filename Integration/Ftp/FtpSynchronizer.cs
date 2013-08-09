using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EnterpriseDT.Net.Ftp;
using Octopus.Shared.Activities;
using Octopus.Shared.Orchestration.Logging;

namespace Octopus.Shared.Integration.Ftp
{
    public class FtpSynchronizer : IFtpSynchronizer
    {
        public void Synchronize(FtpSynchronizationSettings settings)
        {
            using (var session = new SynchronizationSession(settings))
            {
                session.Execute();
            }
        }

        public class SynchronizationSession : IDisposable
        {
            readonly static HashSet<string> IgnoredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            readonly FtpSynchronizationSettings settings;
            readonly SecureFTPConnection ftpConnection;
            readonly ITrace log;
            CancellationTokenRegistration cancelRegistration;

            static SynchronizationSession()
            {
                IgnoredFiles.Add("PreDeploy.ps1");
                IgnoredFiles.Add("Deploy.ps1");
                IgnoredFiles.Add("PostDeploy.ps1");
                IgnoredFiles.Add("DeployFailed.ps1");
            }

            public SynchronizationSession(FtpSynchronizationSettings settings)
            {
                this.settings = settings;

                ftpConnection = new SecureFTPConnection();
                ftpConnection.LicenseOwner = "OctopusDeploy";
                ftpConnection.LicenseKey = "064-6432-3419-5486";
                ftpConnection.Protocol = settings.UseFtps ? FileTransferProtocol.FTPSExplicit : FileTransferProtocol.FTP;
                ftpConnection.ServerAddress = settings.Host;
                ftpConnection.UserName = settings.Username;
                ftpConnection.Password = settings.Password;
                ftpConnection.AutoLogin = true;
                ftpConnection.ServerValidation = SecureFTPServerValidationType.None;
                if (settings.Port >= 1)
                {
                    ftpConnection.ServerPort = settings.Port;
                }

                ftpConnection.Synchronized += OnSynchronized;
                ftpConnection.CommandSent += OnCommandSent;
                ftpConnection.Connecting += OnConnecting;
                ftpConnection.Connected += OnConnected;

                log = settings.Log;

                cancelRegistration = settings.CancellationToken.Register(Cancel);
            }

            public void Execute()
            {
                try
                {
                    ConnectAndSynchronize();
                }
                catch (Exception)
                {
                    if (!settings.CancellationToken.IsCancellationRequested)
                        throw;
                }
            }

            void ConnectAndSynchronize()
            {
                ftpConnection.Connect();

                if (ftpConnection.WelcomeMessage != null)
                    foreach (var message in ftpConnection.WelcomeMessage)
                        Console.WriteLine(message);

                var rules = new FTPSyncRules();
                rules.Direction = TransferDirection.UPLOAD;
                rules.IgnoreCase = true;
                rules.IncludeSubdirectories = true;
                rules.DeleteIfSourceAbsent = settings.DeleteDestinationFiles;
                rules.FilterType = FTPFilterType.Callback;
                rules.FilterCallback = FilterCallback;

                ftpConnection.Synchronize(settings.LocalDirectory, settings.RemoteDirectory, rules);
            }

            bool FilterCallback(FTPFile file)
            {
                var name = Path.GetFileName(file.Name);
                if (name != null && IgnoredFiles.Contains(name))
                {
                    log.Verbose("Excluding " + file.Name + " from upload");
                    return false;
                }

                return true;
            }

            void OnConnecting(object sender, FTPConnectionEventArgs ftpConnectionEventArgs)
            {
                log.Verbose("Connecting...");
            }

            void OnConnected(object sender, FTPConnectionEventArgs e)
            {
                if (e.Exception != null)
                {
                    log.Error("Unable to connect: " + e.Exception.Message);
                }
                else
                {
                    log.Verbose("Connected");
                }
            }

            void OnCommandSent(object sender, FTPMessageEventArgs e)
            {
                log.Verbose(e.Message);
            }

            void OnSynchronized(object sender, FTPSyncEventArgs e)
            {
                log.Verbose("Synchronize complete");

                var results = e.Results;
                if (results != null)
                {
                    log.Verbose("Total operations: " + results.TotalCount);
                }
            }

            void OnClosed(object sender, FTPConnectionEventArgs e)
            {
                log.Verbose("Connection closed");
            }

            void Cancel()
            {
                log.Verbose("Cancelling...");
                Dispose();
            }

            public void Dispose()
            {
                cancelRegistration.Dispose();

                try
                {
                    try
                    {
                        if (ftpConnection.IsTransferring)
                        {
                            ftpConnection.CancelTransfer();
                        }
                    }
                    catch { }

                    ftpConnection.Synchronized -= OnSynchronized;
                    ftpConnection.CommandSent -= OnCommandSent;
                    ftpConnection.Connecting -= OnConnecting;
                    ftpConnection.Connected -= OnConnected;
                    ftpConnection.Closed -= OnClosed;
                    ftpConnection.Dispose();
                }
                catch { }
            }
        }
    }
}