using System.IO;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Services.Scripts.StateStore;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;
using InMemoryLog = Octopus.Tentacle.CommonTestUtils.Diagnostics.InMemoryLog;

namespace Octopus.Tentacle.Tests.Scripts
{
    [TestFixture]
    public class ScriptStateStoreTests
    {
        [Test]
        public void WeCanRoundTrip()
        {
            var octopusFileSystem = new OctopusPhysicalFileSystem(new InMemoryLog());
            using var tmpDir = new TemporaryDirectory(octopusFileSystem);
            var scriptStateStore = new ScriptStateStore(octopusFileSystem, s => Path.Combine(tmpDir.DirectoryPath, s));
            var scriptState = scriptStateStore.Create();
            scriptState.Start();
            scriptState.Complete(99, true);
            
            scriptStateStore.Save(scriptState);

            var loaded = scriptStateStore.Load();

            loaded.Should().BeEquivalentTo(scriptState);
        }

        const string OldScriptState = @"{""Created"":""2025-04-09T23:24:03.683243+00:00"",""Started"":""2025-04-09T23:24:03.731068+00:00"",""Completed"":""2025-04-09T23:24:03.731175+00:00"",""State"":2,""ExitCode"":99,""RanToCompletion"":true}";
        
        [Test]
        public void WeCanReadOldScriptStates()
        {
            var octopusFileSystem = new OctopusPhysicalFileSystem(new InMemoryLog());
            using var tmpDir = new TemporaryDirectory(octopusFileSystem);
            var scriptStateStore = new ScriptStateStore(octopusFileSystem, s => Path.Combine(tmpDir.DirectoryPath, s));
            
            File.WriteAllText(Path.Combine(tmpDir.DirectoryPath, "scriptstate.json"), OldScriptState);
            
            var loaded = scriptStateStore.Load();

            loaded.RanToCompletion.Should().BeTrue();
            loaded.ExitCode.Should().Be(99);
            loaded.State.Should().Be(ProcessState.Complete);
        }
    }
}