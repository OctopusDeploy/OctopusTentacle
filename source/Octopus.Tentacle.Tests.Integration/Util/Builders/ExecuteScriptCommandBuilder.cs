using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Builders;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public class ExecuteScriptCommandBuilder
    {
        readonly List<ScriptFile> files = new();
        readonly List<string> arguments = new();
        readonly Dictionary<ScriptType, string> additionalScripts = new();
        StringBuilder scriptBody = new(string.Empty);
        ScriptIsolationLevel isolation = ScriptIsolationLevel.FullIsolation;
        TimeSpan scriptIsolationMutexTimeout = TimeSpan.FromMilliseconds(int.MaxValue);
        string scriptIsolationMutexName = "RunningScript";
        string taskId = Guid.NewGuid().ToString();
        ScriptTicket scriptTicket = new UniqueScriptTicketBuilder().Build();
        TimeSpan? durationStartScriptCanWaitForScriptToFinish = TimeSpan.FromSeconds(5);

        public ExecuteScriptCommandBuilder()
        {
            // Set defaults for the tests
            WithIsolation(ScriptIsolationLevel.NoIsolation);
            WithDurationStartScriptCanWaitForScriptToFinish(null);
        }

        public ExecuteScriptCommandBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public ExecuteScriptCommandBuilder WithAdditionalScriptTypes(ScriptType scriptType, string scriptBody)
        {
            additionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public ExecuteScriptCommandBuilder WithIsolation(ScriptIsolationLevel isolation)
        {
            this.isolation = isolation;
            return this;
        }

        public ExecuteScriptCommandBuilder WithFiles(params ScriptFile[]? files)
        {
            if (files != null)
                this.files.AddRange(files);

            return this;
        }

        public ExecuteScriptCommandBuilder WithArguments(params string[]? arguments)
        {
            if (arguments != null)
                this.arguments.AddRange(arguments);

            return this;
        }

        public ExecuteScriptCommandBuilder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public ExecuteScriptCommandBuilder WithMutexName(string name)
        {
            scriptIsolationMutexName = name;
            return this;
        }

        public ExecuteScriptCommandBuilder WithTaskId(string taskId)
        {
            this.taskId = taskId;
            return this;
        }

        public ExecuteScriptCommandBuilder WithScriptTicket(ScriptTicket scriptTicket)
        {
            this.scriptTicket = scriptTicket;
            return this;
        }

        public ExecuteScriptCommandBuilder WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan? duration)
        {
            this.durationStartScriptCanWaitForScriptToFinish = duration;
            return this;
        }

        public ExecuteScriptCommand Build()
            => new(scriptTicket,
                taskId,
                scriptBody.ToString(), arguments.ToArray(), isolation, scriptIsolationMutexTimeout, scriptIsolationMutexName, durationStartScriptCanWaitForScriptToFinish, additionalScripts, files.ToArray());
    }
}