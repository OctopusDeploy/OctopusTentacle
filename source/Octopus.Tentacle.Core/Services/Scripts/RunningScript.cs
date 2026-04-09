using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Core.Services.Scripts.Logging;
using Octopus.Tentacle.Core.Services.Scripts.PowerShellStartup;
using Octopus.Tentacle.Core.Services.Scripts.Shell;
using Octopus.Tentacle.Core.Services.Scripts.StateStore;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Core.Services.Scripts
{
    public class RunningScript: IRunningScript
    {
        readonly IScriptWorkspace workspace;
        readonly IScriptStateStore? stateStore;
        readonly IShell shell;
        readonly string taskId;
        readonly CancellationToken runningScriptToken;
        readonly IReadOnlyDictionary<string, string> environmentVariables;
        readonly ILog log;
        readonly ScriptIsolationMutex scriptIsolationMutex;
        readonly TimeSpan powerShellStartupTimeout;

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptStateStore? stateStore,
            IScriptLog scriptLog,
            string taskId,
            ScriptIsolationMutex scriptIsolationMutex,
            CancellationToken runningScriptToken,
            IReadOnlyDictionary<string, string> environmentVariables,
            TimeSpan powerShellStartupTimeout,
            ILog log
            )
        {
            this.shell = shell;
            this.workspace = workspace;
            this.stateStore = stateStore;
            this.taskId = taskId;
            this.runningScriptToken = runningScriptToken;
            this.environmentVariables = environmentVariables;
            this.log = log;
            this.scriptIsolationMutex = scriptIsolationMutex;
            this.ScriptLog = scriptLog;
            this.State = ProcessState.Pending;
            this.powerShellStartupTimeout = powerShellStartupTimeout;
        }

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            string taskId,
            ScriptIsolationMutex scriptIsolationMutex,
            CancellationToken runningScriptToken,
            IReadOnlyDictionary<string, string> environmentVariables,
            TimeSpan powerShellStartupTimeout,
            ILog log) : this(shell, workspace, null, scriptLog, taskId, scriptIsolationMutex, runningScriptToken, environmentVariables, powerShellStartupTimeout, log)
        {
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }

        public IScriptLog ScriptLog { get; }
        public Task Cleanup(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task Execute()
        {
            var exitCode = -1;

            try
            {
                var shellPath = shell.GetFullPath();

                using (var writer = ScriptLog.CreateWriter())
                {
                    try
                    {
                        using (scriptIsolationMutex.Acquire(workspace.IsolationLevel,
                                   workspace.ScriptMutexAcquireTimeout,
                                   workspace.ScriptMutexName ?? nameof(RunningScript),
                                   message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                                   taskId,
                                   runningScriptToken,
                                   log))
                        {
                            State = ProcessState.Running;

                            RecordScriptHasStarted(writer);

                            exitCode = workspace.ShouldMonitorPowerShellStartup()
                                ? await RunPowershellScriptWithMonitoring(shellPath, writer, runningScriptToken)
                                : RunScript(shellPath, writer, runningScriptToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution canceled.");
                        exitCode = ScriptExitCodes.CanceledExitCode;
                    }
                    catch (TimeoutException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution timed out.");
                        exitCode = ScriptExitCodes.TimeoutExitCode;
                    }
                }
            }
            catch (Exception)
            {
                // Something went really really wrong, probably creating or writing to the log file (Disk space)
                exitCode = ScriptExitCodes.FatalExitCode;
            }
            finally
            {
                try
                {
                    RecordScriptHasCompleted(exitCode);
                }
                finally
                {
                    ExitCode = exitCode;
                    State = ProcessState.Complete;
                }
            }
        }

        async Task<int> RunPowershellScriptWithMonitoring(string shellPath, IScriptLogWriter writer, CancellationToken runningScriptToken)
        {
            // We want to be able to make some effort to cancel the running script, if the monitor task detects it as hung.
            // Hence, we make a linked cancellation token with the runningScriptToken 
            await using var scriptTaskCts = new CancelOnDisposeCancellationToken(runningScriptToken);
            
            // The monitoring task is NOT linked to the runningScriptToken, since it should keep monitoring even if an attempt to
            // cancel the script is made. Remember, these hung powershell scripts WILL NOT CANCEL, so we must continue to monitor.
            // Note: We don't bother reacting to the runningScriptToken, since under normal circumstances cancellation will be
            //       strictly after the script has started. Additionally, scripts can be killed. The only case that reacting to
            //       the runningScriptToken would help is when we are in those situations where the script never starts AND won't
            //       respond to being killed. The Additional effort doesn't seem worth it.
            await using var monitoringTaskCts = new CancelOnDisposeCancellationToken();
            
            var monitor = new PowerShellStartupMonitor(workspace.WorkingDirectory, powerShellStartupTimeout, log, taskId);
            
            var monitoringTask = monitor.WaitForStartup(monitoringTaskCts.Token);
            var scriptTask = Task.Run(() => RunScript(shellPath, writer, scriptTaskCts.Token), scriptTaskCts.Token);
            
            var completedTask = await Task.WhenAny(monitoringTask, scriptTask);
            
            if (completedTask == monitoringTask)
            {
                var startupStatus = await monitoringTask;
                
                if (startupStatus == PowerShellStartupStatus.NeverStarted)
                {
                    // PowerShell never started - exit immediately with appropriate code
                    writer.WriteOutput(ProcessOutputSource.StdErr, 
                        $"{shellPath} process did not start within {powerShellStartupTimeout.TotalMinutes} minutes. Script execution aborted.");
                    
                    // The script has not started, and the files on disk have been arranged, so it will never meaningfully progress.
                    // We will now abandon the script, as we do we will cancell its cancellation token. Which will result in
                    // the script possibly dieing, although from what we have seen, the script will never die.
                    return ScriptExitCodes.PowerShellNeverStartedExitCode;
                }
            }

            var exitCode = await scriptTask;
            
            return exitCode;
        }
        
        void RecordScriptHasStarted(IScriptLogWriter writer)
        {
            try
            {
                if (stateStore != null)
                {
                    var scriptState = stateStore.Load();
                    scriptState.Start();
                    stateStore.Save(scriptState);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    writer.WriteOutput(ProcessOutputSource.StdOut, $"Warning: An exception occurred saving the ScriptState: {ex.Message}");
                    writer.WriteOutput(ProcessOutputSource.StdOut, ex.ToString());
                }
                catch
                {
                }
            }
        }

        void RecordScriptHasCompleted(int exitCode)
        {
            try
            {
                if (stateStore != null)
                {
                    var scriptState = stateStore.Load();
                    scriptState.Complete(exitCode);
                    stateStore.Save(scriptState);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    using var writer = ScriptLog.CreateWriter();
                    writer.WriteOutput(ProcessOutputSource.StdOut, $"Warning: An exception occurred saving the ScriptState: {ex.Message}");
                    writer.WriteOutput(ProcessOutputSource.StdOut, ex.ToString());
                }
                catch
                {
                }
            }
        }

        int RunScript(string shellPath, IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            try
            {
                var exitCode = SilentProcessRunner.ExecuteCommand(
                    shellPath,
                    shell.FormatCommandArguments(workspace.BootstrapScriptFilePath, workspace.ScriptArguments, false),
                    workspace.WorkingDirectory,
                    LogScriptOutputTo(writer, ProcessOutputSource.Debug),
                    LogScriptOutputTo(writer, ProcessOutputSource.StdOut),
                    LogScriptOutputTo(writer, ProcessOutputSource.StdErr),
                    environmentVariables,
                    cancellationToken);

                return exitCode;
            }
            catch (Exception ex)
            {
                writer.WriteOutput(ProcessOutputSource.StdErr, "An exception was thrown when invoking " + shellPath + ": " + ex.Message);
                writer.WriteOutput(ProcessOutputSource.StdErr, ex.ToString());

                return ScriptExitCodes.PowershellInvocationErrorExitCode;
            }
        }

        Action<string> LogScriptOutputTo(IScriptLogWriter logOutput, ProcessOutputSource level)
        {
            try
            {
                return output => logOutput.WriteOutput(level, output);
            }
            catch (Exception e)
            {
                log.Warn(e, $"Could not write script output to log, for task {taskId}");
                throw;
            }
        }
    }
}