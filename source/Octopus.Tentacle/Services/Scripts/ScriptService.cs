using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Shared.Contracts;

using Octopus.Shared.Scripts;
using Octopus.Shared.Security;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration.Proxy;

namespace Octopus.Tentacle.Services.Scripts
{
    [Service]
    public class ScriptService : IScriptService
    {
        readonly IShell shell;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IOctopusFileSystem fileSystem;
        readonly ISensitiveValueMasker sensitiveValueMasker;
        readonly ConcurrentDictionary<string, RunningScript> running = new ConcurrentDictionary<string, RunningScript>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.OrdinalIgnoreCase);

        public ScriptService(IShell shell, IScriptWorkspaceFactory workspaceFactory, IOctopusFileSystem fileSystem, ISensitiveValueMasker sensitiveValueMasker)
        {
            this.shell = shell;
            this.workspaceFactory = workspaceFactory;
            this.fileSystem = fileSystem;
            this.sensitiveValueMasker = sensitiveValueMasker;
        }

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            var ticket = ScriptTicket.Create(command.TaskId);
            var workspace = PrepareWorkspace(command, ticket);
            var cancel = new CancellationTokenSource();
            var process = LaunchShell(ticket, command.TaskId ?? ticket.TaskId, workspace, cancel);
            running.TryAdd(ticket.TaskId, process);
            cancellationTokens.TryAdd(ticket.TaskId, cancel);
            return ticket;
        }

        IScriptWorkspace PrepareWorkspace(StartScriptCommand command, ScriptTicket ticket)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
            workspace.IsolationLevel = command.Isolation;
            workspace.ScriptMutexAcquireTimeout = command.ScriptIsolationMutexTimeout;
            workspace.ScriptArguments = command.Arguments;

            if (PlatformDetection.IsRunningOnNix || PlatformDetection.IsRunningOnNix)
            {
                //TODO: This could be better
                workspace.BootstrapScript(command.Scripts.ContainsKey(ScriptType.Bash)
                    ? command.Scripts[ScriptType.Bash]
                    : command.ScriptBody);
            }
            else
            {
                workspace.BootstrapScript(command.ScriptBody);
            }

            command.Files.ForEach(file => SaveFileToDisk(workspace, file));

            return workspace;
        }

        void SaveFileToDisk(IScriptWorkspace workspace, ScriptFile scriptFile)
        {
            if (scriptFile.EncryptionPassword == null)
            {
                scriptFile.Contents.Receiver().SaveTo(workspace.ResolvePath(scriptFile.Name));
            }
            else
            {
                scriptFile.Contents.Receiver().Read(stream =>
                {
                    using (var reader = new StreamReader(stream))
                    {
                        fileSystem.WriteAllBytes(workspace.ResolvePath(scriptFile.Name), new AesEncryption(scriptFile.EncryptionPassword).Encrypt(reader.ReadToEnd()));
                    }
                });
            }
        }

        IScriptLog CreateLog(IScriptWorkspace workspace)
        {
            return new ScriptLog(workspace.ResolvePath("Output.log"), fileSystem, sensitiveValueMasker);
        }

        RunningScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancel)
        {
            var runningScript = new RunningScript(shell, workspace, CreateLog(workspace), serverTaskId, cancel.Token);
            var thread = new Thread(runningScript.Execute) {Name = "Executing PowerShell script for " + ticket.TaskId};
            thread.Start();
            return runningScript;
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            RunningScript script;
            running.TryGetValue(request.Ticket.TaskId, out script);
            return GetResponse(request.Ticket, script, request.LastLogSequence);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            CancellationTokenSource cancel;
            if (cancellationTokens.TryGetValue(command.Ticket.TaskId, out cancel))
            {
                cancel.Cancel();
            }

            RunningScript script;
            running.TryGetValue(command.Ticket.TaskId, out script);
            return GetResponse(command.Ticket, script, command.LastLogSequence);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            RunningScript script;
            CancellationTokenSource cancellation;
            running.TryRemove(command.Ticket.TaskId, out script);
            cancellationTokens.TryRemove(command.Ticket.TaskId, out cancellation);
            var response = GetResponse(command.Ticket, script, command.LastLogSequence);
            var workspace = workspaceFactory.GetWorkspace(command.Ticket);
            workspace.Delete();
            return response;
        }

        ScriptStatusResponse GetResponse(ScriptTicket ticket, RunningScript script, long lastLogSequence)
        {
            var exitCode = script != null ? script.ExitCode : 0;
            var state = script != null ? script.State : ProcessState.Complete;
            var log = script != null ? script.Log : CreateLog(workspaceFactory.GetWorkspace(ticket));

            long next;
            var logs = log.GetOutput(lastLogSequence, out next);
            return new ScriptStatusResponse(ticket, state, exitCode, logs, next);
        }
    }
}
