using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Controls
{
    public class ServiceWatcher : ViewModel, IDisposable
    {
        readonly string instanceName;
        readonly string executablePath;
        readonly Timer timer;
        bool canRestart;
        bool canStart;
        bool canStop;
        string statusSummary;
        string runningAs;
        bool isNotRunning;
        bool isRunning;
        bool isInstalled;
        bool isBusy;

        public ServiceWatcher(ApplicationName application, string instanceName, string executablePath)
        {
            this.instanceName = instanceName;
            ServiceName = Octopus.Tentacle.Configuration.ServiceName.GetWindowsServiceName(application, instanceName);
            this.executablePath = executablePath;

            timer = new Timer(500);
            timer.Elapsed += (sender, args) => OnTimerElapsed();
            timer.Start();
            OnTimerElapsed();
        }

        public string ServiceName { get; set; }

        public bool IsInstalled
        {
            get => isInstalled;
            set
            {
                if (value.Equals(isInstalled)) return;
                isInstalled = value;
                OnPropertyChanged();
            }
        }

        public bool IsRunning
        {
            get => isRunning;
            set
            {
                if (value.Equals(isRunning)) return;
                isRunning = value;
                OnPropertyChanged();
            }
        }

        public bool IsNotRunning
        {
            get => isNotRunning;
            set
            {
                if (value.Equals(isNotRunning)) return;
                isNotRunning = value;
                OnPropertyChanged();
            }
        }

        public string RunningAs
        {
            get => runningAs;
            set
            {
                if (value == runningAs) return;
                runningAs = value;
                OnPropertyChanged();
            }
        }

        public string StatusSummary
        {
            get => statusSummary;
            set
            {
                if (value == statusSummary) return;
                statusSummary = value;
                OnPropertyChanged();
            }
        }

        public bool CanStart
        {
            get => canStart;
            set
            {
                if (value.Equals(canStart)) return;
                canStart = value;
                OnPropertyChanged();
            }
        }

        public bool CanStop
        {
            get => canStop;
            set
            {
                if (value.Equals(canStop)) return;
                canStop = value;
                OnPropertyChanged();
            }
        }

        public bool CanRestart
        {
            get => canRestart;
            set
            {
                if (value.Equals(canRestart)) return;
                canRestart = value;
                OnPropertyChanged();
            }
        }

        public IEnumerable<CommandLineInvocation> GetStartCommands()
        {
            yield return BuildCommand("service --instance --start");
        }

        public IEnumerable<CommandLineInvocation> GetStopCommands()
        {
            yield return BuildCommand("service --instance --stop");
        }

        public IEnumerable<CommandLineInvocation> GetRepairCommands()
        {
            yield return BuildCommand("service --instance --stop", ignoreFailedExitCode: true);
            yield return BuildCommand("service --instance --uninstall", ignoreFailedExitCode: true);
            yield return BuildCommand("service --instance --install --start");
        }

        public IEnumerable<CommandLineInvocation> GetReconfigureCommands(params string[] commandsToRunAfterStopping)
        {
            yield return BuildCommand("service --instance --stop", ignoreFailedExitCode: true);
            foreach (var c in commandsToRunAfterStopping) yield return BuildCommand(c);
            yield return BuildCommand("service --instance --install --start");
        }

        public IEnumerable<CommandLineInvocation> GetRestartCommands()
        {
            yield return BuildCommand("service --instance --stop --start");
        }

        /// <summary>
        /// Builds a command-line invocation for you based on simple arguments. Note: --instance will be replaced with the correctly formatted instance name argument.
        /// </summary>
        /// <param name="arguments">The list of arguments for the command-line, typically something like "service --instance --start". Note: --instance will be replaced with the correctly formatted instance name argument.</param>
        /// <param name="systemArguments">Any extra arguments you want to use.</param>
        /// <param name="ignoreFailedExitCode">What it says on the box.</param>
        public CommandLineInvocation BuildCommand(string arguments, string systemArguments = null, bool ignoreFailedExitCode = false)
        {
            return new CommandLineInvocation(executablePath, arguments.Replace("--instance", $"--instance \"{instanceName}\""), systemArguments) { IgnoreFailedExitCode = ignoreFailedExitCode};
        }

        void OnTimerElapsed()
        {
            if (isBusy)
                return;

            try
            {
                isBusy = true;

#pragma warning disable CA1416
                var serviceController = ServiceController.GetServices().FirstOrDefault(x => x.DisplayName == ServiceName || x.ServiceName == ServiceName);
#pragma warning restore CA1416
                if (serviceController == null)
                {
                    IsInstalled = false;
                    IsRunning = false;
                    IsNotRunning = true;
                    CanStart = false;
                    CanStop = false;
                    CanRestart = false;
                    StatusSummary = "is not installed";
                    return;
                }

                IsInstalled = true;

#pragma warning disable CA1416
                switch (serviceController.Status)
#pragma warning restore CA1416
                {
#pragma warning disable CA1416
                    case ServiceControllerStatus.ContinuePending:
#pragma warning restore CA1416
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending continuation";
                        break;
#pragma warning disable CA1416
                    case ServiceControllerStatus.Paused:
#pragma warning restore CA1416
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "paused";
                        break;
#pragma warning disable CA1416
                    case ServiceControllerStatus.PausePending:
#pragma warning restore CA1416
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending pause";
                        break;
#pragma warning disable CA1416
                    case ServiceControllerStatus.Running:
#pragma warning restore CA1416
                        IsRunning = true;
                        IsNotRunning = false;
                        CanStart = false;
                        CanStop = true;
                        CanRestart = true;
                        StatusSummary = "running";
                        break;
#pragma warning disable CA1416
                    case ServiceControllerStatus.StartPending:
#pragma warning restore CA1416
                        IsRunning = true;
                        IsNotRunning = false;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending start";
                        break;
#pragma warning disable CA1416
                    case ServiceControllerStatus.Stopped:
#pragma warning restore CA1416
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = true;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "stopped";
                        break;
#pragma warning disable CA1416
                    case ServiceControllerStatus.StopPending:
#pragma warning restore CA1416
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending stop";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                StatusSummary = ex.PrettyPrint(false);
                IsRunning = false;
                IsNotRunning = false;
                CanStart = false;
                CanStop = false;
                CanRestart = false;
            }
            finally
            {
                isBusy = false;
            }
        }

        public void Dispose()
        {
            timer.Stop();
        }
    }
}
