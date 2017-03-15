using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Shared.Scripts;

namespace Octopus.Shared.Contracts
{
    public class StartScriptCommandBuilder
    {
        StringBuilder scriptBody = new StringBuilder(string.Empty);

        ScriptIsolationLevel isolation = ScriptIsolationLevel.FullIsolation;

        readonly List<ScriptFile> files = new List<ScriptFile>();

        readonly List<string> arguments = new List<string>();

        TimeSpan scriptIsolationMutexTimeout = ScriptIsolationMutex.NoTimeout;

        public StartScriptCommandBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public StartScriptCommandBuilder WithReplacementInScriptBody(string oldValue, string newValue)
        {
            scriptBody.Replace(oldValue, newValue);
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
            if (arguments != null)
            {
                this.arguments.AddRange(arguments);
            }

            return this;
        }

        public StartScriptCommandBuilder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public StartScriptCommand Build()
        {
            return new StartScriptCommand(scriptBody.ToString(), isolation, scriptIsolationMutexTimeout, arguments.ToArray(), files.ToArray());
        }
    }
}