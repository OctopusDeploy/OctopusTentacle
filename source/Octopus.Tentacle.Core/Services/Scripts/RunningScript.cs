using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Core.Services.Scripts.Logging;
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
        readonly CancellationToken token;
        readonly IReadOnlyDictionary<string, string> environmentVariables;
        readonly ILog log;
        readonly ScriptIsolationMutex scriptIsolationMutex;
        readonly TimeSpan powerShellStartupCheckDelay;

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptStateStore? stateStore,
            IScriptLog scriptLog,
            string taskId,
            ScriptIsolationMutex scriptIsolationMutex,
            CancellationToken token,
            IReadOnlyDictionary<string, string> environmentVariables,
            ILog log,
            TimeSpan? powerShellStartupCheckDelay = null)
        {
            this.shell = shell;
            this.workspace = workspace;
            this.stateStore = stateStore;
            this.taskId = taskId;
            this.token = token;
            this.environmentVariables = environmentVariables;
            this.log = log;
            this.scriptIsolationMutex = scriptIsolationMutex;
            this.ScriptLog = scriptLog;
            this.State = ProcessState.Pending;
            this.powerShellStartupCheckDelay = powerShellStartupCheckDelay ?? TimeSpan.FromMinutes(5);
        }

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            string taskId,
            ScriptIsolationMutex scriptIsolationMutex,
            CancellationToken token,
            IReadOnlyDictionary<string, string> environmentVariables,
            ILog log) : this(shell, workspace, null, scriptLog, taskId, scriptIsolationMutex, token, environmentVariables, log)
        {
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }

        public IScriptLog ScriptLog { get; }
        public Task Cleanup(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Execute()
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
                                   token,
                                   log))
                        {
                            State = ProcessState.Running;

                            RecordScriptHasStarted(writer);

                            exitCode = RunScriptWithMonitoring(shellPath, writer).GetAwaiter().GetResult();
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

        async Task<int> RunScriptWithMonitoring(string shellPath, IScriptLogWriter writer)
        {
            // Create a linked cancellation token that we can cancel when exiting early
            await using var cts = new CancelOnDisposeCancellationToken(token);
            var cancelOnDisposeToken = cts.Token;
            
            // Start PowerShell startup monitoring if applicable
            var monitoringTask = StartPowerShellStartupMonitoring(writer, cancelOnDisposeToken);
            
            // Start script execution
            var scriptTask = Task.Run(() => RunScript(shellPath, writer), cancelOnDisposeToken);
            
            // Race between monitoring and script execution
            var completedTask = await Task.WhenAny(monitoringTask, scriptTask);
            
            if (completedTask == monitoringTask)
            {
                // Monitoring task completed first
                var startupStatus = await monitoringTask;
                
                if (startupStatus == PowerShellStartupStatus.NeverStarted)
                {
                    // PowerShell never started - exit immediately with appropriate code
                    writer.WriteOutput(ProcessOutputSource.StdErr, 
                        $"PowerShell process did not start within {powerShellStartupCheckDelay.TotalMinutes} minutes. " +
                        "Script execution aborted.");
                    
                    // Clean up should-run file
                    CleanupShouldRunFile();
                    
                    return ScriptExitCodes.PowerShellNeverStartedExitCode;
                }
            }

            // Script completed first
            var exitCode = await scriptTask;
            
            return exitCode;
        }
        
        Task<PowerShellStartupStatus> StartPowerShellStartupMonitoring(IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            var shouldRunFilePath = PowerShellStartupDetection.GetShouldRunFilePath(workspace.WorkingDirectory);
            
            // Only start monitoring if the should-run file exists (meaning detection is enabled)
            if (!File.Exists(shouldRunFilePath))
            {
                return Task.FromResult(PowerShellStartupStatus.NotMonitored);
            }
            
            return Task.Run(async () =>
            {
                try
                {
                    await DelayWithoutException.Delay(powerShellStartupCheckDelay, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return PowerShellStartupStatus.NotMonitored;
                    }
                    
                    // Try to create the started file
                    try
                    {
                        var startedFilePath = PowerShellStartupDetection.GetStartedFilePath(workspace.WorkingDirectory);
                        using var fileStream = File.Open(startedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        // Successfully created the file, meaning PowerShell never started
                        log.Warn($"PowerShell startup detection: PowerShell did not start within {powerShellStartupCheckDelay.TotalMinutes} minutes for task {taskId}");
                            
                        return PowerShellStartupStatus.NeverStarted;
                    }
                    catch (IOException)
                    {
                        // File already exists, meaning PowerShell did start (just very slowly)
                        log.Info($"PowerShell startup detection: PowerShell started late (after {powerShellStartupCheckDelay.TotalMinutes} minutes) for task {taskId}");
                        return PowerShellStartupStatus.Started;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Task was cancelled, this is expected
                    return PowerShellStartupStatus.NotMonitored;
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"Error in PowerShell startup monitoring for task {taskId}");
                    return PowerShellStartupStatus.NotMonitored;
                }
            });
        }
        
        void CleanupShouldRunFile()
        {
            try
            {
                var shouldRunFilePath = PowerShellStartupDetection.GetShouldRunFilePath(workspace.WorkingDirectory);
                if (File.Exists(shouldRunFilePath))
                {
                    File.Delete(shouldRunFilePath);
                }
            }
            catch (Exception ex)
            {
                log.Warn(ex, $"Failed to delete should-run file for task {taskId}");
            }
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

        int RunScript(string shellPath, IScriptLogWriter writer)
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
                    token);

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