using System;
using System.Collections.Generic;
using System.Text;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.CommonTestUtils.Builders
{
    public class StartScriptCommandV3AlphaBuilder
    {
        readonly List<ScriptFile> files = new List<ScriptFile>();
        readonly List<string> arguments = new List<string>();
        readonly Dictionary<ScriptType, string> additionalScripts = new Dictionary<ScriptType, string>();
        StringBuilder scriptBody = new StringBuilder(string.Empty);
        ScriptIsolationLevel isolation = ScriptIsolationLevel.NoIsolation;
        TimeSpan scriptIsolationMutexTimeout = ScriptIsolationMutex.NoTimeout;
        string scriptIsolationMutexName = nameof(RunningScript);
        string taskId = Guid.NewGuid().ToString();
        ScriptTicket scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());
        TimeSpan? durationStartScriptCanWaitForScriptToFinish;
        IScriptExecutionContext executionContext = new LocalShellScriptExecutionContext();

        public StartScriptCommandV3AlphaBuilder WithScriptBody(string scriptBody)
        {
            this.scriptBody = new StringBuilder(scriptBody);
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithScriptBodyForCurrentOs(string windowsScript, string bashScript)
        {
            this.scriptBody = new StringBuilder(PlatformDetection.IsRunningOnWindows ? windowsScript : bashScript);
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithScriptBody(ScriptBuilder scriptBuilder)
        {
            scriptBody = new StringBuilder(scriptBuilder.BuildForCurrentOs());
            return this;
        }

        public StartScriptCommandV3AlphaBuilder WithScriptBody(Action<ScriptBuilder> builderFunc)
        {
            var scriptBuilder = new ScriptBuilder();
            builderFunc(scriptBuilder);
            return WithScriptBody(scriptBuilder);
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
            => new StartScriptCommandV3Alpha(scriptBody.ToString(),
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