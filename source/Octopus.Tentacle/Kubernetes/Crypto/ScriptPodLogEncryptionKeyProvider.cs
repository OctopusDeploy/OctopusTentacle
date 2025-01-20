using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes.Crypto
{
    public interface IScriptPodLogEncryptionKeyProvider
    {
        Task GenerateAndWriteEncryptionKeyfileToWorkspace(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<byte[]> GetEncryptionKey(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        void Delete(ScriptTicket scriptTicket);
    }

    public class ScriptPodLogEncryptionKeyProvider : IScriptPodLogEncryptionKeyProvider
    {
        const int KeyLengthInBytes = 32;
        const string Filename = "keyfile";
        readonly IScriptWorkspaceFactory scriptWorkspaceFactory;
        readonly IScriptPodLogEncryptionKeyGenerator encryptionKeyGenerator;
        readonly ISystemLog log;

        readonly ConcurrentDictionary<ScriptTicket, byte[]> encryptionKeyCache = new();

        public ScriptPodLogEncryptionKeyProvider(IScriptWorkspaceFactory scriptWorkspaceFactory, IScriptPodLogEncryptionKeyGenerator encryptionKeyGenerator, ISystemLog log)
        {
            this.scriptWorkspaceFactory = scriptWorkspaceFactory;
            this.encryptionKeyGenerator = encryptionKeyGenerator;
            this.log = log;
        }

        public async Task GenerateAndWriteEncryptionKeyfileToWorkspace(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            if (encryptionKeyCache.ContainsKey(scriptTicket))
            {
                throw new PodLogEncryptionKeyException($"An encryption key already exists for script {scriptTicket.TaskId}");
            }
            
            var workspace = scriptWorkspaceFactory.GetWorkspace(scriptTicket);
            await GenerateAndWriteEncryptionKeyfileToWorkspace(scriptTicket, workspace, cancellationToken);
        }

        async Task<byte[]> GenerateAndWriteEncryptionKeyfileToWorkspace(ScriptTicket scriptTicket, IScriptWorkspace workspace,CancellationToken cancellationToken)
        {
            log.Verbose($"Generating log encryption key for script pod {scriptTicket.TaskId}");
            var encryptionKeyBytes = await encryptionKeyGenerator.GenerateKey(scriptTicket, KeyLengthInBytes, cancellationToken);
            if (!encryptionKeyCache.TryAdd(scriptTicket, encryptionKeyBytes))
            {
                throw new PodLogEncryptionKeyException($"Failed to store encryption key in memory cache for script {scriptTicket.TaskId}");
            }

            try
            {
                var fileContents = Convert.ToBase64String(encryptionKeyBytes);
                workspace.WriteFile(Filename, fileContents);
                return encryptionKeyBytes;
            }
            catch (Exception e)
            {
                throw new PodLogEncryptionKeyException($"Failed to write encryption key to workspace for script {scriptTicket.TaskId}", e);
            }
        }

        public async Task<byte[]> GetEncryptionKey(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            if (encryptionKeyCache.TryGetValue(scriptTicket, out var keyBytes))
            {
                return keyBytes;
            }

            var workspace = scriptWorkspaceFactory.GetWorkspace(scriptTicket);
            var fileContents = workspace.TryReadFile(Filename);
            //If we can't load the encryption key from the filesystem
            if (fileContents == null)
            {
                //regenerate the encryption key, write to the filesystem and return the key
                return await GenerateAndWriteEncryptionKeyfileToWorkspace(scriptTicket, workspace, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(fileContents))
            {
                throw new PodLogEncryptionKeyException($"Encryption key loaded from workspace for script {scriptTicket.TaskId} is empty or whitespace");
            }

            var encryptionKeyBytes = Convert.FromBase64String(fileContents);
            if (!encryptionKeyCache.TryAdd(scriptTicket, encryptionKeyBytes))
            {
                throw new PodLogEncryptionKeyException($"Failed to store encryption key in memory cache for script {scriptTicket.TaskId}");
            }

            return encryptionKeyBytes;
        }

        public void Delete(ScriptTicket scriptTicket)
        {
            encryptionKeyCache.TryRemove(scriptTicket, out _);
        }
    }

    public class PodLogEncryptionKeyException : Exception
    {
        public PodLogEncryptionKeyException(string message) : base(message)
        {
        }

        public PodLogEncryptionKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}