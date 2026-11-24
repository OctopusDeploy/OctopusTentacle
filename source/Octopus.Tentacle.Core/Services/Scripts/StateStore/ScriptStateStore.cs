using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Core.Services.Scripts.StateStore
{
    public class ScriptStateStore : IScriptStateStore
    {
        readonly SemaphoreSlim scriptStateLock = new(1, 1);
        readonly Func<string, string> pathResolver;
        readonly IOctopusFileSystem fileSystem;

        public ScriptStateStore(IScriptWorkspace workspace, IOctopusFileSystem fileSystem)
        : this(fileSystem, workspace.ResolvePath)
        {
        }

        public ScriptStateStore(IOctopusFileSystem fileSystem, Func<string, string> pathResolver)
        {
            this.fileSystem = fileSystem;
            this.pathResolver = pathResolver;
        }

        string StateFilePath => pathResolver("scriptstate.json");

        public ScriptState Create()
        {
            using var _ = scriptStateLock.Lock();

            if (ExistsNoLock())
            {
                throw new InvalidOperationException($"ScriptState already exists at {StateFilePath}");
            }

            var state = new ScriptState(DateTimeOffset.UtcNow);
            var serialized = SerializeState(state);

            // Write the state to a temporary file and move it into place, so Tentacle stopping part way through can't
            // leave the state file empty.
            var tempFilePath = pathResolver(Guid.NewGuid().ToString());
            using (var writer = GetStreamWriter(tempFilePath, FileMode.CreateNew))
            {
                writer.Write(serialized);
            }

            // An empty state file is left by a Create from an earlier Tentacle version that was interrupted.
            if (fileSystem.FileExists(StateFilePath))
            {
                fileSystem.DeleteFile(StateFilePath);
            }

            File.Move(tempFilePath, StateFilePath);

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
            var tempFilePath = pathResolver(Guid.NewGuid().ToString());
            var backupPath = pathResolver(Guid.NewGuid().ToString());
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
            // Earlier Tentacle versions created the state file before writing to it, so Tentacle stopping in between left
            // it empty (https://github.com/OctopusDeploy/OctopusTentacle/issues/659). The script never started then, so
            // treat an empty file as no state.
            if (!fileSystem.FileExists(StateFilePath))
            {
                return false;
            }

            // The workspace can be deleted in between, e.g. by a CompleteScript running alongside a status request.
            try
            {
                return fileSystem.GetFileSize(StateFilePath) > 0;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
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