﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Maintenance;
using Octopus.Tentacle.Core.Services;
using Octopus.Tentacle.Core.Services.Scripts;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Core.Services.Scripts.Shell;
using Octopus.Tentacle.Maintenance;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Services.Scripts
{
    [Service(typeof(IScriptService))]
    public class ScriptService : IAsyncScriptService, IRunningScriptReporter
    {
        readonly IShell shell;
        readonly IScriptWorkspaceFactory workspaceFactory;
        readonly ISystemLog log;
        readonly ConcurrentDictionary<string, RunningScript> running = new(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentDictionary<string, CancellationTokenSource> cancellationTokens = new(StringComparer.OrdinalIgnoreCase);
        ScriptIsolationMutex scriptIsolationMutex;

        public ScriptService(
            IShell shell,
            IScriptWorkspaceFactory workspaceFactory,
            ScriptIsolationMutex scriptIsolationMutex,
            ISystemLog log)
        {
            this.shell = shell;
            this.workspaceFactory = workspaceFactory;
            this.log = log;
            this.scriptIsolationMutex = scriptIsolationMutex;
        }

        public async Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, CancellationToken cancellationToken)
        {
            var ticket = ScriptTicketFactory.Create(command.TaskId);
            var workspace = await workspaceFactory.PrepareWorkspace(ticket,
                command.ScriptBody,
                command.Scripts,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName,
                command.Arguments,
                command.Files,
                cancellationToken);

            var cancel = new CancellationTokenSource();
            var process = LaunchShell(ticket, command.TaskId ?? ticket.TaskId, workspace, cancel);
            running.TryAdd(ticket.TaskId, process);
            cancellationTokens.TryAdd(ticket.TaskId, cancel);
            return ticket;
        }

        public async Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            running.TryGetValue(request.Ticket.TaskId, out var script);
            return GetResponse(request.Ticket, script, request.LastLogSequence);
        }

        public async Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            if (cancellationTokens.TryGetValue(command.Ticket.TaskId, out var cancel))
            {
                cancel.Cancel();
            }

            running.TryGetValue(command.Ticket.TaskId, out var script);
            return GetResponse(command.Ticket, script, command.LastLogSequence);
        }

        public async Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            running.TryRemove(command.Ticket.TaskId, out var script);
            cancellationTokens.TryRemove(command.Ticket.TaskId, out _);
            var response = GetResponse(command.Ticket, script, command.LastLogSequence);
            var workspace = workspaceFactory.GetWorkspace(command.Ticket);
            await workspace.Delete(cancellationToken);
            return response;
        }

        RunningScript LaunchShell(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancel)
        {
            var runningScript = new RunningScript(shell, workspace, workspace.CreateLog(), serverTaskId, scriptIsolationMutex, cancel.Token, new Dictionary<string, string>(), log);
            var thread = new Thread(runningScript.Execute) { Name = "Executing PowerShell script for " + ticket.TaskId };
            thread.Start();
            return runningScript;
        }

        ScriptStatusResponse GetResponse(ScriptTicket ticket, RunningScript? script, long lastLogSequence)
        {
            var exitCode = script != null ? script.ExitCode : 0;
            var state = script != null ? script.State : ProcessState.Complete;
            var scriptLog = script != null ? script.ScriptLog : workspaceFactory.GetWorkspace(ticket).CreateLog();

            var logs = scriptLog.GetOutput(lastLogSequence, out var next);
            return new ScriptStatusResponse(ticket, state, exitCode, logs, next);
        }

        public bool IsRunningScript(ScriptTicket ticket)
        {
            if (running.TryGetValue(ticket.TaskId, out var script))
            {
                if (script.State != ProcessState.Complete)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
