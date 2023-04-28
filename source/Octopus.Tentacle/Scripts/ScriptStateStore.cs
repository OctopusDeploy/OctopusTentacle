using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptStateStore : IScriptStateStore
    {
        readonly SemaphoreSlim storeLock = new(1, 1);
        readonly SemaphoreSlim scriptStateLock = new(1, 1);
        readonly IScriptWorkspace workspace;
        readonly ScriptTicket scriptTicket;
        readonly IOctopusFileSystem fileSystem;

        public ScriptStateStore(ScriptTicket scriptTicket, IScriptWorkspace workspace, IOctopusFileSystem fileSystem)
        {
            this.scriptTicket = scriptTicket;
            this.workspace = workspace;
            this.fileSystem = fileSystem;
        }

        string StateFilePath => workspace.ResolvePath("scriptstate.json");

        public ScriptState Create()
        {
            scriptStateLock.Wait();

            try
            {
                if (ExistsNoLock())
                {
                    throw new InvalidOperationException($"ScriptState already exists at {StateFilePath}");
                }

                var state = new ScriptState(scriptTicket);
                var serialized = SerializeState(state);
                using var writer = GetStreamWriter(FileMode.CreateNew);
                writer.Write(serialized);

                return state;
            }
            finally
            {
                scriptStateLock.Release();
            }
        }

        public ScriptState Load()
        {
            scriptStateLock.Wait();

            try
            {
                if (!ExistsNoLock())
                {
                    throw new InvalidOperationException($"ScriptState does not exists at {StateFilePath}");
                }

                using var reader = new StreamReader(fileSystem.OpenFile(StateFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite));
                var serialized = reader.ReadToEnd();
                var state = DeserializeState(serialized);

                if (state == null)
                {
                    throw new Exception($"ScriptState could not be loaded from {StateFilePath}");
                }

                return state;
            }
            finally
            {
                scriptStateLock.Release();
            }
        }

        public void Save(ScriptState state)
        {
            scriptStateLock.Wait();

            try
            {
                if (!ExistsNoLock())
                {
                    throw new InvalidOperationException($"ScriptState does not exists at {StateFilePath}");
                }

                var serialized = SerializeState(state);
                using var writer = GetStreamWriter(FileMode.Create);
                writer.Write(serialized);
            }
            finally
            {
                scriptStateLock.Release();
            }
        }

        public bool Exists()
        {
            scriptStateLock.Wait();
            try
            {
                return ExistsNoLock();

            }
            finally
            {
                scriptStateLock.Release();
            }
        }

        public IDisposable GetExclusiveLock()
        {
            storeLock.Wait();
            return new SemaphoreSlimReleaser(storeLock);
        }

        bool ExistsNoLock()
        {
            return fileSystem.FileExists(StateFilePath);
        }

        ScriptState? DeserializeState(string serialized)
        {
            return JsonConvert.DeserializeObject<ScriptState>(serialized);
        }

        string SerializeState(ScriptState state)
        {
            return JsonConvert.SerializeObject(state);
        }

        private StreamWriter GetStreamWriter(FileMode fileMode)
        {
            return new StreamWriter(fileSystem.OpenFile(StateFilePath, fileMode, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite));
        }
    }
}