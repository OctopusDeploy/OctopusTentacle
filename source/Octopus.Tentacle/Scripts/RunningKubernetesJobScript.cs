using System;
using System.Linq;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class RunningKubernetesJobScript : IRunningScript
    {
        private readonly IShell shell;
        private readonly IScriptWorkspace workspace;
        private readonly ScriptTicket ticket;
        private readonly string taskId;
        private readonly CancellationToken cancellationToken;
        private readonly ILog log;

        public RunningKubernetesJobScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            ScriptTicket ticket,
            string taskId,
            CancellationToken cancellationToken,
            ILog log)
        {
            this.shell = shell;
            this.workspace = workspace;
            this.ticket = ticket;
            this.taskId = taskId;
            this.cancellationToken = cancellationToken;
            this.log = log;
            ScriptLog = scriptLog;
        }

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public void Execute()
        {
            var exitCode = -1;
            try
            {
                using (var writer = ScriptLog.CreateWriter())
                {
                    try
                    {
                        using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                                   workspace.ScriptMutexAcquireTimeout,
                                   workspace.ScriptMutexName ?? nameof(RunningShellScript),
                                   message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                                   taskId,
                                   cancellationToken,
                                   log))
                        {
                            State = ProcessState.Running;

                            var jobBootstrapScriptName = CreateJobBootstrapScript();

                            exitCode = RunAndMonitorKubernetesJob(jobBootstrapScriptName, writer);
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
                exitCode = ScriptExitCodes.FatalExitCode;
            }
            finally
            {
                ExitCode = exitCode;
                State = ProcessState.Complete;
            }
        }

        private int RunAndMonitorKubernetesJob(string jobBootstrapScriptName, IScriptLogWriter writer)
        {
            var exitCode = SilentProcessRunner.ExecuteCommand(
                shell.GetFullPath(),
                shell.FormatCommandArguments(jobBootstrapScriptName, null, false),
                workspace.WorkingDirectory,
                output => writer.WriteOutput(ProcessOutputSource.Debug, output),
                output => writer.WriteOutput(ProcessOutputSource.StdOut, output),
                output => writer.WriteOutput(ProcessOutputSource.StdErr, output),
                cancellationToken);

            return exitCode;
        }

        private string CreateJobBootstrapScript()
        {
            var jobYaml = BuildJobYaml();

            var path = workspace.WriteFile("KubernetesJob.yaml", jobYaml);

            //for windows we need to
            if (PlatformDetection.IsRunningOnWindows)
            {
                var jobBootstrapScript = $@"
Write-Host ""##octopus[stdout-verbose]""
Write-Host ""{jobYaml}""
Write-Host ""##octopus[stdout-default]""

& kubectl apply -f ""{path}""

if ((Test-Path variable:global:LastExitCode))
{{
	Exit $LastExitCode
}}
";

                var jobBootstrapPath = workspace.WriteFile("JobBootstrap.ps1", jobBootstrapScript);

                return jobBootstrapPath;
            }

            throw new NotImplementedException("I only have a windows machine, so that'll do for now :D");
        }

        private string BuildJobYaml()
        {
            var escapedBootstrapFile = workspace.BootstrapScriptFilePath.Replace("'", "''");
            var boostrapFileArg = new[]
            {
                $"\"{escapedBootstrapFile}\""
            };

            var escapedArgs = (workspace.ScriptArguments ?? Array.Empty<string>())
                .Select(arg => $"\"{arg}\"")
                .ToList();

            var scriptArgs = string.Join(", ", boostrapFileArg.Concat(escapedArgs));

            return @$"
apiVersion: batch/v1
kind: Job
metadata:
    name: ""{ticket.TaskId}""
    labels: 
        ""serverTaskId"": ""{taskId}""
spec:
    template:
        spec:
            containers:
            - name: octopus-deploy-worker-tools
              image: octopusdeploy/worker-tools:ubuntu.22.04
              command: [""{Bash.GetFullBashPath()}""]
              args: [{scriptArgs}]
            restartPolicy: never
    backoffLimit: 4
";
        }
    }
}