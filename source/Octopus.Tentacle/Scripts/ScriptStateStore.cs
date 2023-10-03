using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class ScriptStateStore : IScriptStateStore
    {
        readonly SemaphoreSlim scriptStateLock = new(1, 1);
        readonly IScriptWorkspace workspace;
        readonly IOctopusFileSystem fileSystem;

        public ScriptStateStore(IScriptWorkspace workspace, IOctopusFileSystem fileSystem)
        {
            this.workspace = workspace;
            this.fileSystem = fileSystem;
        }

        string StateFilePath => workspace.ResolvePath("scriptstate.json");

        public ScriptState Create()
        {
            using var _ = scriptStateLock.Lock();

            if (ExistsNoLock())
            {
                throw new InvalidOperationException($"ScriptState already exists at {StateFilePath}");
            }

            var state = new ScriptState(DateTimeOffset.UtcNow);
            var serialized = SerializeState(state);
            using var writer = GetStreamWriter(StateFilePath, FileMode.CreateNew);
            writer.Write(serialized);

            return state;
        }

        public ScriptState Load()
        {
            using var _ = scriptStateLock.Lock();

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

        public void Save(ScriptState state)
        {
            using var _ = scriptStateLock.Lock();

            if (!ExistsNoLock())
            {
                throw new InvalidOperationException($"ScriptState does not exists at {StateFilePath}");
            }

            var serialized = SerializeState(state);
            var tempFilePath = workspace.ResolvePath(Guid.NewGuid().ToString());
            var backupPath = workspace.ResolvePath(Guid.NewGuid().ToString());
            using (var writer = GetStreamWriter(tempFilePath, FileMode.Create))
            {
                writer.Write(serialized);
            }

            File.Replace(tempFilePath, StateFilePath, backupPath, true);
        }

        public bool Exists()
        {
            using var _ = scriptStateLock.Lock();

            return ExistsNoLock();
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

        private StreamWriter GetStreamWriter(string path, FileMode fileMode)
        {
            return new StreamWriter(fileSystem.OpenFile(path, fileMode, FileAccess.Write, FileShare.Delete | FileShare.ReadWrite));
        }
    }
}