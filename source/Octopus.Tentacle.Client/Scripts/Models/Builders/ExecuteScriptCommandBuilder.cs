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
        protected ScriptTicket ScriptTicket { get; private set; }
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

        public ExecuteScriptCommandBuilder WithScriptTicket(ScriptTicket scriptTicket)
        {
            this.ScriptTicket = scriptTicket;
            return this;
        }
        
        public ExecuteScriptCommandBuilder WithScriptBody(string scriptBody)
        {
            ScriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public ExecuteScriptCommandBuilder ReplaceInScriptBody(string oldValue, string newValue)
        {
            ScriptBody.Replace(oldValue, newValue);
            return this;
        }

        public ExecuteScriptCommandBuilder WithAdditionalScriptType(ScriptType scriptType, string scriptBody)
        {
            AdditionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public ExecuteScriptCommandBuilder WithIsolationLevel(ScriptIsolationLevel isolation)
        {
            IsolationConfiguration = IsolationConfiguration with { IsolationLevel= isolation };
            return this;
        }

        public ExecuteScriptCommandBuilder WithScriptFile(ScriptFile scriptFile)
        {
            Files.Add(scriptFile);
            return this;
        }

        public ExecuteScriptCommandBuilder WithNoIsolationLevel() => WithIsolationLevel(ScriptIsolationLevel.NoIsolation);
        public ExecuteScriptCommandBuilder WithFullIsolationLevel() => WithIsolationLevel(ScriptIsolationLevel.FullIsolation);

        public ExecuteScriptCommandBuilder WithFiles(IEnumerable<ScriptFile>? files)
        {
            Files.Clear();
            if (files is not null)
            {
                Files.AddRange(files);
            }

            return this;
        }

        public ExecuteScriptCommandBuilder WithArguments(IEnumerable<string>? arguments)
        {
            Arguments.Clear();
            if (arguments is not null)
            {
                Arguments.AddRange(arguments);
            }

            return this;
        }

        public ExecuteScriptCommandBuilder WithIsolationMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            IsolationConfiguration = IsolationConfiguration with { MutexTimeout= scriptIsolationMutexTimeout };
            return this;
        }

        public ExecuteScriptCommandBuilder WithNoIsolationMutexTimeout() => WithIsolationMutexTimeout(ScriptIsolationConfiguration.NoTimeout);

        public ExecuteScriptCommandBuilder WithIsolationMutexName(string name)
        {
            IsolationConfiguration = IsolationConfiguration with { MutexName= name };
            return this;
        }

        public abstract ExecuteScriptCommand Build();
    }
}