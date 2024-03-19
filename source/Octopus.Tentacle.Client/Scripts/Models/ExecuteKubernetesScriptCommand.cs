using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ExecuteKubernetesScriptCommand : ExecuteScriptCommand
    {
        public ExecuteKubernetesScriptCommand(
            ScriptTicket scriptTicket,
            string taskId,
            string scriptBody,
            string[] arguments,
            ScriptIsolationLevel isolationLevel,
            TimeSpan isolationMutexTimeout,
            string? isolationMutexName,
            Dictionary<ScriptType, string>? additionalScripts,
            ScriptFile[] additionalFiles,
            string? image,
            string? feedUrl,
            string? feedUsername,
            string? feedPassword)
            : base(scriptTicket, taskId, scriptBody, arguments, isolationLevel, isolationMutexTimeout, isolationMutexName, null, additionalScripts, additionalFiles)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public string? Image { get; }
        public string? FeedUrl { get; }
        public string? FeedUsername { get; }
        public string? FeedPassword { get; }
    }
}