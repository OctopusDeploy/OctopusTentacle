using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Contracts.Builders
{
    public class StartScriptCommandV3AlphaBuilder
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
        IScriptExecutionContext executionContext = new LocalShellScriptExecutionContext();

        public StartScriptCommandV3AlphaBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithAdditionalScriptTypes(ScriptType scriptType, string scriptBody)
        {
            additionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithIsolation(ScriptIsolationLevel isolation)
        {
            this.isolation = isolation;
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithFiles(params ScriptFile[] files)
        {
            if (files != null)
                this.files.AddRange(files);

            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithArguments(params string[] arguments)
        {
            if (arguments != null)
                this.arguments.AddRange(arguments);

            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithMutexName(string name)
        {
            scriptIsolationMutexName = name;
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithTaskId(string taskId)
        {
            this.taskId = taskId;
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithScriptTicket(ScriptTicket scriptTicket)
        {
            this.scriptTicket = scriptTicket;
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan? duration)
        {
            this.durationStartScriptCanWaitForScriptToFinish = duration;
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithExecutionContext(IScriptExecutionContext executionContext)
        {
            this.executionContext = executionContext;
            return this;
        }

        public StartScriptCommandV3Alpha Build()
            => new(scriptBody.ToString(),
                isolation,
                scriptIsolationMutexTimeout,
                scriptIsolationMutexName,
                arguments.ToArray(),
                taskId,
                scriptTicket,
                durationStartScriptCanWaitForScriptToFinish,
                executionContext,
                additionalScripts,
                files.ToArray());
    }
}