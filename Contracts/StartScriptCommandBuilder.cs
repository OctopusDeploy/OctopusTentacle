using System;
using System.Collections.Generic;
using Octopus.Shared.Scripts;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommandBuilder
    {
        string scriptBody = string.Empty;

        ScriptIsolationLevel isolation = ScriptIsolationLevel.FullIsolation;

        readonly List<ScriptFile> files = new List<ScriptFile>();

        string[] arguments = new string[0];

        TimeSpan scriptIsolationMutexTimeout = ScriptIsolationMutex.NoTimeout;

        public StartScriptCommandBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = scriptBody;
            return this;
        }

        public StartScriptCommandBuilder WithIsolation(ScriptIsolationLevel isolation)
        {
            this.isolation = isolation;
            return this;
        }

        public StartScriptCommandBuilder WithFiles(params ScriptFile[] files)
        {
            if (files != null)
            {
                this.files.AddRange(files);
            }
            
            return this;
        }

        public StartScriptCommandBuilder WithArguments(params string[] arguments)
        {
            this.arguments = arguments;
            return this;
        }

        public StartScriptCommandBuilder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public StartScriptCommand Build()
        {
            return new StartScriptCommand(scriptBody, isolation, scriptIsolationMutexTimeout, arguments, files.ToArray());
        }
    }
}