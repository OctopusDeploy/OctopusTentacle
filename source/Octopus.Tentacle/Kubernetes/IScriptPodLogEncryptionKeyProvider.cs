using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Scripts;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IScriptPodLogEncryptionKeyProvider
    {
        void WriteEncryptionKeyfileToWorkspace(ScriptTicket scriptTicket);
        byte[] GetEncryptionKey(ScriptTicket scriptTicket);
        void Delete(ScriptTicket scriptTicket);
    }

    public class ScriptPodLogEncryptionKeyProvider : IScriptPodLogEncryptionKeyProvider
    {
        const string Filename = "keyfile";
        readonly IScriptWorkspaceFactory scriptWorkspaceFactory;

        readonly ConcurrentDictionary<ScriptTicket, byte[]> encryptionKeyCache = new();

        public ScriptPodLogEncryptionKeyProvider(IScriptWorkspaceFactory scriptWorkspaceFactory)
        {
            this.scriptWorkspaceFactory = scriptWorkspaceFactory;
        }

        public void WriteEncryptionKeyfileToWorkspace(ScriptTicket scriptTicket)
        {
            if (encryptionKeyCache.ContainsKey(scriptTicket))
            {
                throw new PodLogEncryptionKeyException($"An encryption key already exists for script {scriptTicket.TaskId}");
            }

            var encryptionKeyBytes = GenerateEncryptionKeyBytes();
            if (!encryptionKeyCache.TryAdd(scriptTicket, encryptionKeyBytes))
            {
                throw new PodLogEncryptionKeyException($"Failed to store encryption key in memory cache for script {scriptTicket.TaskId}");
            }

            try
            {
                var workspace = scriptWorkspaceFactory.GetWorkspace(scriptTicket);
                var fileContents = Convert.ToBase64String(encryptionKeyBytes);
                workspace.WriteFile(Filename, fileContents);
            }
            catch (Exception e)
            {
                throw new PodLogEncryptionKeyException($"Failed to write encryption key to workspace for script {scriptTicket.TaskId}", e);
            }
        }

        public byte[] GetEncryptionKey(ScriptTicket scriptTicket)
        {
            if (encryptionKeyCache.TryGetValue(scriptTicket, out var keyBytes))
            {
                return keyBytes;
            }

            //read from file
            var workspace = scriptWorkspaceFactory.GetWorkspace(scriptTicket);
            var fileContents = workspace.TryReadFile(Filename);
            if (fileContents == null)
            {
                throw new PodLogEncryptionKeyException($"Failed to load encryption key from workspace for script {scriptTicket.TaskId}");
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

#if NETFRAMEWORK
        static readonly RNGCryptoServiceProvider RandomCryptoServiceProvider = new RNGCryptoServiceProvider();
#endif
        static byte[] GenerateEncryptionKeyBytes()
        {
            //A 32-byte key results in AES-256 being used
            const int keyLength = 32;
#if NETFRAMEWORK
            var buffer = new byte[keyLength];
            RandomCryptoServiceProvider.GetBytes(buffer);
            return buffer;
#else
            return RandomNumberGenerator.GetBytes(keyLength);
#endif
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