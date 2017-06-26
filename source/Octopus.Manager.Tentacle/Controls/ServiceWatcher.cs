﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Timers;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;

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
            ServiceName = Shared.Configuration.ServiceName.GetWindowsServiceName(application, instanceName);
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
        public CommandLineInvocation BuildCommand(string arguments, string systemArguments = "--nologo --console", bool ignoreFailedExitCode = false)
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

                var serviceController = ServiceController.GetServices().FirstOrDefault(x => x.DisplayName == ServiceName || x.ServiceName == ServiceName);
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

                switch (serviceController.Status)
                {
                    case ServiceControllerStatus.ContinuePending:
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending continuation";
                        break;
                    case ServiceControllerStatus.Paused:
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "paused";
                        break;
                    case ServiceControllerStatus.PausePending:
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending pause";
                        break;
                    case ServiceControllerStatus.Running:
                        IsRunning = true;
                        IsNotRunning = false;
                        CanStart = false;
                        CanStop = true;
                        CanRestart = true;
                        StatusSummary = "running";
                        break;
                    case ServiceControllerStatus.StartPending:
                        IsRunning = true;
                        IsNotRunning = false;
                        CanStart = false;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "pending start";
                        break;
                    case ServiceControllerStatus.Stopped:
                        IsRunning = false;
                        IsNotRunning = true;
                        CanStart = true;
                        CanStop = false;
                        CanRestart = false;
                        StatusSummary = "stopped";
                        break;
                    case ServiceControllerStatus.StopPending:
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