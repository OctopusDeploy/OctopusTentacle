using System;
using System.Collections.Concurrent;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.Scripts
{
    [Service]
    public class ScriptService : IScriptService
    {
        readonly IShell shell;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly IOctopusFileSystem fileSystem;
        readonly SensitiveValueMasker sensitiveValueMasker;
        readonly ISystemLog log;
        readonly ConcurrentDictionary<string, RunningScript> running = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokens = new(StringComparer.OrdinalIgnoreCase);

        public ScriptService(
            IShell shell,
            IScriptWorkspaceFactory workspaceFactory,
            IOctopusFileSystem fileSystem,
            SensitiveValueMasker sensitiveValueMasker,
            ISystemLog log)
        {
            this.shell = shell;
            this.workspaceFactory = workspaceFactory;
            this.fileSystem = fileSystem;
            this.sensitiveValueMasker = sensitiveValueMasker;
            this.log = log;
        }

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            var ticket = ScriptTicketFactory.Create(command.TaskId);
            var workspace = workspaceFactory.PrepareWorkspace(command, ticket);
            var cancel = new CancellationTokenSource();
            var process = LaunchShell(ticket, command.TaskId ?? ticket.TaskId, workspace, cancel);
            running.TryAdd(ticket.TaskId, process);
            cancellationTokens.TryAdd(ticket.TaskId, cancel);
            return ticket;
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request)
        {
            running.TryGetValue(request.Ticket.TaskId, out var script);
            return GetResponse(request.Ticket, script, request.LastLogSequence);
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command)
        {
            if (cancellationTokens.TryGetValue(command.Ticket.TaskId, out var cancel))
            {
                cancel.Cancel();
            }

            running.TryGetValue(command.Ticket.TaskId, out var script);
            return GetResponse(command.Ticket, script, command.LastLogSequence);
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command)
        {
            running.TryRemove(command.Ticket.TaskId, out var script);
            cancellationTokens.TryRemove(command.Ticket.TaskId, out _);
            var response = GetResponse(command.Ticket, script, command.LastLogSequence);
            var workspace = workspaceFactory.GetWorkspace(command.Ticket);
            workspace.Delete();
            return response;
        }

        RunningScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancel)
        {
            var runningScript = new RunningScript(shell, workspace, CreateLog(workspace), serverTaskId, cancel.Token, log);
            var thread = new Thread(runningScript.Execute) { Name = "Executing PowerShell script for " + ticket.TaskId };
            thread.Start();
            return runningScript;
        }

        IScriptLog CreateLog(IScriptWorkspace workspace)
        {
            return new ScriptLog(workspace.ResolvePath("Output.log"), fileSystem, sensitiveValueMasker);
        }

        ScriptStatusResponse GetResponse(ScriptTicket ticket, RunningScript? script, long lastLogSequence)
        {
            var exitCode = script != null ? script.ExitCode : 0;
            var state = script != null ? script.State : ProcessState.Complete;
            var scriptLog = script != null ? script.ScriptLog : CreateLog(workspaceFactory.GetWorkspace(ticket));

            var logs = scriptLog.GetOutput(lastLogSequence, out var next);
            return new ScriptStatusResponse(ticket, state, exitCode, logs, next);
        }
    }
}
