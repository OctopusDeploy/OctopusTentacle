using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Builders;

namespace Octopus.Tentacle.Client.Scripts.Models.Builders
{
    public abstract class ExecuteScriptCommandBuilder
    {
        protected List<ScriptFile> Files { get; private set; } = new();
        protected List<string> Arguments { get; private set; } = new();
        protected Dictionary<ScriptType, string> AdditionalScripts { get; private set; } = new();
        protected StringBuilder ScriptBody { get; private set; } = new(string.Empty);
        protected string TaskId { get; }
        protected ScriptTicket ScriptTicket { get; }
        protected ScriptIsolationConfiguration IsolationConfiguration { get; private set; }

        protected ExecuteScriptCommandBuilder(string taskId, ScriptIsolationLevel defaultIsolationLevel)
        {
            TaskId = taskId;
            ScriptTicket = new UniqueScriptTicketBuilder().Build();

            IsolationConfiguration = new ScriptIsolationConfiguration(
                defaultIsolationLevel,
                "RunningScript",
                ScriptIsolationConfiguration.NoTimeout);
        }

        public ExecuteScriptCommandBuilder SetScriptBody(string scriptBody)
        {
            ScriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public ExecuteScriptCommandBuilder ReplaceInScriptBody(string oldValue, string newValue)
        {
            ScriptBody.Replace(oldValue, newValue);
            return this;
        }

        public ExecuteScriptCommandBuilder AddAdditionalScriptType(ScriptType scriptType, string scriptBody)
        {
            AdditionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public ExecuteScriptCommandBuilder SetIsolationLevel(ScriptIsolationLevel isolation)
        {
            IsolationConfiguration = IsolationConfiguration with { IsolationLevel= isolation };
            return this;
        }

        public ExecuteScriptCommandBuilder SetNoIsolationLevel() => SetIsolationLevel(ScriptIsolationLevel.NoIsolation);
        public ExecuteScriptCommandBuilder SetFullIsolationLevel() => SetIsolationLevel(ScriptIsolationLevel.FullIsolation);

        public ExecuteScriptCommandBuilder SetFiles(IEnumerable<ScriptFile>? files)
        {
            Files.Clear();
            if (files is not null)
            {
                Files.AddRange(files);
            }

            return this;
        }

        public ExecuteScriptCommandBuilder SetArguments(IEnumerable<string>? arguments)
        {
            Arguments.Clear();
            if (arguments is not null)
            {
                Arguments.AddRange(arguments);
            }

            return this;
        }

        public ExecuteScriptCommandBuilder SetIsolationMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            IsolationConfiguration = IsolationConfiguration with { MutexTimeout= scriptIsolationMutexTimeout };
            return this;
        }

        public ExecuteScriptCommandBuilder SetNoIsolationMutexTimeout() => SetIsolationMutexTimeout(ScriptIsolationConfiguration.NoTimeout);

        public ExecuteScriptCommandBuilder SetIsolationMutexName(string name)
        {
            IsolationConfiguration = IsolationConfiguration with { MutexName= name };
            return this;
        }

        public abstract ExecuteScriptCommand Build();


        public ExecuteScriptCommandBuilder AddFile(ScriptFile scriptFile)
        {
            Files.Add(scriptFile);
            return this;
        }
    }
}