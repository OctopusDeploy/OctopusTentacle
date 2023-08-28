using System;
using System.Collections.Concurrent;
using System.Threading;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Services.Scripts
{
    [Service]
    public class ScriptService : IScriptService
    {
        private readonly IScriptExecutor scriptExecutor;
        readonly IShell shell;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly ISystemLog log;
        readonly ConcurrentDictionary<string, IRunningScript> running = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokens = new(StringComparer.OrdinalIgnoreCase);

        public ScriptService(
            IScriptExecutor scriptExecutor,
            IShell shell,
            IScriptWorkspaceFactory workspaceFactory,
            ISystemLog log)
        {
            this.scriptExecutor = scriptExecutor;
            this.shell = shell;
            this.workspaceFactory = workspaceFactory;
            this.log = log;
        }

        public ScriptTicket StartScript(StartScriptCommand command)
        {
            var ticket = ScriptTicketFactory.Create(command.TaskId);
            var workspace = workspaceFactory.PrepareWorkspace(ticket,
                command.ScriptBody,
                command.Scripts,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName,
                command.Arguments,
                command.Files);

            var cancel = new CancellationTokenSource();

            //execute the script
            var process = scriptExecutor.Execute(ticket, command.TaskId ?? ticket.TaskId, workspace, cancel);

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

        ScriptStatusResponse GetResponse(ScriptTicket ticket, IRunningScript? script, long lastLogSequence)
        {
            var exitCode = script != null ? script.ExitCode : 0;
            var state = script != null ? script.State : ProcessState.Complete;
            var scriptLog = script != null ? script.ScriptLog : workspaceFactory.GetWorkspace(ticket).CreateLog();

            var logs = scriptLog.GetOutput(lastLogSequence, out var next);
            return new ScriptStatusResponse(ticket, state, exitCode, logs, next);
        }
    }
}
