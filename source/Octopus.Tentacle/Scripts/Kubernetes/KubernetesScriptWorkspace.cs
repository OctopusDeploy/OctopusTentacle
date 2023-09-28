using System;
using System.Text;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts.Kubernetes
{
    public class KubernetesScriptWorkspace : ScriptWorkspace
    {
        private bool isLoadingInfo;
        private bool isSavingInfo;

        public KubernetesScriptWorkspace(ScriptTicket scriptTicket, string workingDirectory, IOctopusFileSystem fileSystem, SensitiveValueMasker sensitiveValueMasker)
            : base(scriptTicket, workingDirectory, fileSystem, sensitiveValueMasker)
        {
        }

        protected override string BootstrapScriptName
            => !PlatformDetection.IsRunningOnWindows
                ? "Bootstrap.sh"
                : base.BootstrapScriptName;

        public override void BootstrapScript(string scriptBody)
        {
            scriptBody = scriptBody.Replace("\r\n", "\n");
            FileSystem.OverwriteFile(BootstrapScriptFilePath, scriptBody, Encoding.Default);
        }

        public override ScriptIsolationLevel IsolationLevel
        {
            get
            {
                LoadWorkspaceInfo();
                return base.IsolationLevel;
            }
            set
            {
                base.IsolationLevel = value;
                SaveWorkspaceInfo();
            }
        }

        public override string[]? ScriptArguments
        {
            get
            {
                LoadWorkspaceInfo();
                return base.ScriptArguments;
            }
            set
            {
                base.ScriptArguments = value;
                SaveWorkspaceInfo();
            }
        }

        public override string? ScriptMutexName
        {
            get
            {
                LoadWorkspaceInfo();
                return base.ScriptMutexName;
            }
            set
            {
                base.ScriptMutexName = value;
                SaveWorkspaceInfo();
            }
        }

        public override TimeSpan ScriptMutexAcquireTimeout
        {
            get
            {
                LoadWorkspaceInfo();
                return base.ScriptMutexAcquireTimeout;
            }
            set
            {
                base.ScriptMutexAcquireTimeout = value;
                SaveWorkspaceInfo();
            }
        }

        private void SaveWorkspaceInfo()
        {
            if (isLoadingInfo)
                return;

            isSavingInfo = true;

            var storedInfo = new StoredInfo
            {
                IsolationLevel = IsolationLevel,
                ScriptArguments = ScriptArguments,
                ScriptMutexName = ScriptMutexName,
                ScriptMutexAcquireTimeout = ScriptMutexAcquireTimeout
            };

            var json = JsonConvert.SerializeObject(storedInfo);

            FileSystem.WriteAllText(ResolvePath("workspaceInfo.json"), json);

            isSavingInfo = false;
        }

        private void LoadWorkspaceInfo()
        {
            if (isSavingInfo)
                return;

            isLoadingInfo = true;

            var json = FileSystem.ReadFile(ResolvePath("workspaceInfo.json"));

            var storedInfo = JsonConvert.DeserializeObject<StoredInfo>(json);

            IsolationLevel = storedInfo!.IsolationLevel;
            ScriptArguments = storedInfo.ScriptArguments;
            ScriptMutexName = storedInfo.ScriptMutexName;
            ScriptMutexAcquireTimeout = storedInfo.ScriptMutexAcquireTimeout;

            isLoadingInfo = false;
        }

        private class StoredInfo
        {
            public ScriptIsolationLevel IsolationLevel { get; set; }
            public string[]? ScriptArguments { get; set; }
            public string? ScriptMutexName { get; set; }
            public TimeSpan ScriptMutexAcquireTimeout { get; set; }
        }
    }
}