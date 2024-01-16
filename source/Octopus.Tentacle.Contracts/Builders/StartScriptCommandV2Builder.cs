using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Contracts.Builders
{
    public class StartScriptCommandV2Builder
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

        public StartScriptCommandV2Builder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public StartScriptCommandV2Builder WithAdditionalScriptTypes(ScriptType scriptType, string scriptBody)
        {
            additionalScripts.Add(scriptType, scriptBody);
            return this;
        }

        public StartScriptCommandV2Builder WithIsolation(ScriptIsolationLevel isolation)
        {
            this.isolation = isolation;
            return this;
        }

        public StartScriptCommandV2Builder WithFiles(params ScriptFile[] files)
        {
            if (files != null)
                this.files.AddRange(files);

            return this;
        }

        public StartScriptCommandV2Builder WithArguments(params string[] arguments)
        {
            if (arguments != null)
                this.arguments.AddRange(arguments);

            return this;
        }

        public StartScriptCommandV2Builder WithMutexTimeout(TimeSpan scriptIsolationMutexTimeout)
        {
            this.scriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            return this;
        }

        public StartScriptCommandV2Builder WithMutexName(string name)
        {
            scriptIsolationMutexName = name;
            return this;
        }

        public StartScriptCommandV2Builder WithTaskId(string taskId)
        {
            this.taskId = taskId;
            return this;
        }

        public StartScriptCommandV2Builder WithScriptTicket(ScriptTicket scriptTicket)
        {
            this.scriptTicket = scriptTicket;
            return this;
        }

        public StartScriptCommandV2Builder WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan? duration)
        {
            this.durationStartScriptCanWaitForScriptToFinish = duration;
            return this;
        }

        public StartScriptCommandV2 Build()
            => new(scriptBody.ToString(),
                isolation,
                scriptIsolationMutexTimeout,
                scriptIsolationMutexName,
                arguments.ToArray(),
                taskId,
                scriptTicket,
                durationStartScriptCanWaitForScriptToFinish,
                additionalScripts,
                files.ToArray());
    }
}