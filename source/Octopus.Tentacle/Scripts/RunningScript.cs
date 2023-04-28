using System;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class RunningScript
    {
        readonly IScriptWorkspace workspace;
        readonly IScriptStateStore? stateRepository;
        readonly IShell shell;
        readonly string taskId;
        readonly CancellationToken token;
        readonly ILog log;

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptStateStore? stateRepository,
            IScriptLog scriptLog,
            string taskId,
            CancellationToken token,
            ILog log)
        {
            this.shell = shell;
            this.workspace = workspace;
            this.stateRepository = stateRepository;
            this.taskId = taskId;
            this.token = token;
            this.log = log;
            this.ScriptLog = scriptLog;
            this.State = ProcessState.Pending;
        }

        public RunningScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            string taskId,
            CancellationToken token,
            ILog log) : this(shell, workspace, null, scriptLog, taskId, token, log)
        {
        }

        public ProcessState State { get; private set; }
        public int ExitCode { get; private set; }

        public IScriptLog ScriptLog { get; }

        public void Execute()
        {
            int exitCode;

            try
            {
                var shellPath = shell.GetFullPath();

                using (var writer = ScriptLog.CreateWriter())
                {
                    try
                    {
                        using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                            workspace.ScriptMutexAcquireTimeout,
                            workspace.ScriptMutexName ?? nameof(RunningScript),
                            message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                            taskId,
                            token,
                            log))
                        {
                            State = ProcessState.Running;

                            if (stateRepository != null)
                            {
                                var scriptState = stateRepository.Load();
                                scriptState.Start();
                                stateRepository.Save(scriptState);
                            }

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

            if (stateRepository != null)
            {
                var scriptState = stateRepository.Load();
                scriptState.Complete(exitCode);
                stateRepository.Save(scriptState);
            }

            ExitCode = exitCode;
            State = ProcessState.Complete;
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