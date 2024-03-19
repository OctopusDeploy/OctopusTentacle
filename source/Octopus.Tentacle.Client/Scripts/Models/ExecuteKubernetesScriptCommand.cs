using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts.Models
{
    public class ExecuteKubernetesScriptCommand : ExecuteScriptCommand
    {
        public ExecuteKubernetesScriptCommand(
            string scriptBody,
            string taskId,
            ScriptTicket scriptTicket,
            ScriptIsolationLevel isolation,
            TimeSpan scriptIsolationMutexTimeout,
            string? isolationMutexName,
            string[] arguments,
            Dictionary<ScriptType, string>? additionalScripts,
            string image,
            string? feedUrl,
            string? feedUsername,
            string? feedPassword,
            params ScriptFile[] additionalFiles)
            : base(scriptBody, taskId, scriptTicket, null, isolation, scriptIsolationMutexTimeout, isolationMutexName, arguments, additionalScripts, additionalFiles)
        {
            Image = image;
            FeedUrl = feedUrl;
            FeedUsername = feedUsername;
            FeedPassword = feedPassword;
        }

        public string Image { get; }
        public string? FeedUrl { get; }
        public string? FeedUsername { get; }
        public string? FeedPassword { get; }
    }
}