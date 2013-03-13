using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EnterpriseDT.Net.Ftp;
using Octopus.Shared.Activities;

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
            readonly IActivityLog log;
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
                ftpConnection.Protocol = settings.UseFtps ? FileTransferProtocol.FTPSExplicit : FileTransferProtocol.FTP;
                ftpConnection.ServerAddress = settings.Host;
                ftpConnection.UserName = settings.Username;
                ftpConnection.Password = settings.Password;
                ftpConnection.AutoLogin = true;
                ftpConnection.ServerValidation = SecureFTPServerValidationType.None;

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

                var rules = new FTPSyncRules { Direction = TransferDirection.UPLOAD, IgnoreCase = true, IncludeSubdirectories = true, DeleteIfSourceAbsent = true, FilterType = FTPFilterType.Callback, FilterCallback = FilterCallback };

                ftpConnection.Synchronize(settings.LocalDirectory, settings.RemoteDirectory, rules);
            }

            bool FilterCallback(FTPFile file)
            {
                var name = Path.GetFileName(file.Name);
                if (name != null && IgnoredFiles.Contains(name))
                {
                    log.Debug("Excluding " + file.Name + " from upload");
                    return false;
                }

                return true;
            }

            void OnConnecting(object sender, FTPConnectionEventArgs ftpConnectionEventArgs)
            {
                log.Debug("Connecting...");
            }

            void OnConnected(object sender, FTPConnectionEventArgs e)
            {
                log.Debug("Connected");
            }

            void OnCommandSent(object sender, FTPMessageEventArgs e)
            {
                log.Debug(e.Message);
            }

            void OnSynchronized(object sender, FTPSyncEventArgs e)
            {
                log.Debug("Synchronize complete");

                var results = e.Results;
                if (results != null)
                {
                    log.Debug("Total operations: " + results.TotalCount);
                }
            }

            void OnClosed(object sender, FTPConnectionEventArgs e)
            {
                log.Debug("Connection closed");
            }

            void Cancel()
            {
                log.Debug("Cancelling...");
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