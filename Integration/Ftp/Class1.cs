using System;
using System.Threading;
using Autofac;
using EnterpriseDT.Net.Ftp;
using Octopus.Shared.Activities;

namespace Octopus.Shared.Integration.Ftp
{
    public class FtpModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<FtpSynchronizer>().As<IFtpSynchronizer>();
        }
    }

    public interface IFtpSynchronizer
    {
        void Synchronize(FtpSynchronizationSettings settings);
    }

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
            readonly FtpSynchronizationSettings settings;
            readonly SecureFTPConnection ftpConnection;
            readonly IActivityLog log;
            CancellationTokenRegistration cancelRegistration;

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

                var rules = new FTPSyncRules { Direction = TransferDirection.UPLOAD, IgnoreCase = true, IncludeSubdirectories = true, DeleteIfSourceAbsent = true };

                ftpConnection.Synchronize(settings.LocalDirectory, settings.RemoteDirectory, rules);
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

    public class FtpSynchronizationSettings
    {
        readonly string host;
        readonly string username;
        readonly string password;
        readonly bool useFtps;
        readonly IActivityLog log;
        readonly CancellationToken cancellationToken;

        public FtpSynchronizationSettings(string host, string username, string password, bool useFtps, IActivityLog log, CancellationToken cancellationToken)
        {
            this.host = host;
            this.username = username;
            this.password = password;
            this.useFtps = useFtps;
            this.log = log;
            this.cancellationToken = cancellationToken;
        }

        public bool UseFtps
        {
            get { return useFtps; }
        }

        public string Host
        {
            get { return host; }
        }

        public string Username
        {
            get { return username; }
        }

        public string Password
        {
            get { return password; }
        }

        public IActivityLog Log
        {
            get { return log; }
        }

        public string LocalDirectory { get; set; }
        public string RemoteDirectory { get; set; }

        public CancellationToken CancellationToken
        {
            get { return cancellationToken; }
        }
    }
}