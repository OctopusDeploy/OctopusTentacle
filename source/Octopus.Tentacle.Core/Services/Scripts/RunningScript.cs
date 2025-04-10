﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptStateStore? stateStore,
            IScriptLog scriptLog,
            string taskId,
            ScriptIsolationMutex scriptIsolationMutex,
            CancellationToken token,
            IReadOnlyDictionary<string, string> environmentVariables,
            ILog log)
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

                            exitCode = RunScript(shellPath, writer);
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
                    output => writer.WriteOutput(ProcessOutputSource.Debug, output),
                    output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                    output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
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
    }
}