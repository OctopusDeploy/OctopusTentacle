using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha
{
    public class StartKubernetesScriptCommandV1Alpha
    {
        [JsonConstructor]
        public StartKubernetesScriptCommandV1Alpha(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string? isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,string? image, string? feedUrl, string? feedUsername, string? feedPassword)
        {
            Arguments = arguments;
            TaskId = taskId;
            ScriptTicket = scriptTicket;
            ScriptBody = scriptBody;
            Isolation = isolation;
            ScriptIsolationMutexTimeout = scriptIsolationMutexTimeout;
            IsolationMutexName = isolationMutexName;
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public StartKubernetesScriptCommandV1Alpha(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            string? image,
            string? feedUrl,
            string? feedUsername,
            string? feedPassword,
            params ScriptFile[]? additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicket,
                image,
                feedUrl,
                feedUsername,
                feedPassword)
        {
            if (additionalFiles != null)
                Files.AddRange(additionalFiles);
        }

        public StartKubernetesScriptCommandV1Alpha(string scriptBody,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string isolationMutexName,
            string[] arguments,
            string taskId,
            ScriptTicket scriptTicket,
            string? image,
            string? feedUrl,
            string? feedUsername,
            string? feedPassword,
            Dictionary<ScriptType, string>? additionalScripts,
            params ScriptFile[]? additionalFiles)
            : this(scriptBody,
                isolation,
                scriptIsolationMutexTimeout,
                isolationMutexName,
                arguments,
                taskId,
                scriptTicket,
                image,
                feedUrl,
                feedUsername,
                feedPassword,
                additionalFiles)
        {
            if (additionalScripts == null || !additionalScripts.Any())
                return;

            foreach (var additionalScript in additionalScripts)
            {
                Scripts.Add(additionalScript.Key, additionalScript.Value);
            }
        }

        public string ScriptBody { get; }
        public string TaskId { get; }
        public ScriptTicket ScriptTicket { get; }

        public ScriptIsolationLevel Isolation { get; }
        public TimeSpan ScriptIsolationMutexTimeout { get; }
        public string? IsolationMutexName { get; }

        public Dictionary<ScriptType, string> Scripts { get; } = new();
        public List<ScriptFile> Files { get; } = new();
        public string[] Arguments { get; }

        public string? Image { get; }

        public string? FeedUrl { get; }

        public string? FeedUsername { get; }

        public string? FeedPassword { get; }
    }
}