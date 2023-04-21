using System;
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Util;

namespace Octopus.Tentacle.Tests.Contracts.Legacy
{
    public class CanSerializeAndDeserializeJsonWithLegacyNameSpace
    {
        [Test]
        public void ShouldSerializeLegacyTypeToLegacyNameSpaceAndAssemblies()
        {
            var serializer = CreateJsonSerializer();

            var memoryStream = new MemoryStream();
            var laJson = serializer.ToJson(new HasAThing(new ScriptTicket("foo")));
            laJson.Should().Contain(
                "Octopus.Shared.Contracts.ScriptTicket, Octopus.Shared",
                because: "It should make reference to the old namespace and assembly for backwards compatability with old tentacle or clients of tentacle.");
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJsonThatReferencesOldAssemblies()
        {
            var json = @"{""theThing"":{""$type"":""Octopus.Shared.Contracts.ScriptTicket, Octopus.Shared"",""TaskId"":""foo""}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(ScriptTicket));
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_CancelScriptCommand()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.CancelScriptCommand, Octopus.Shared"",""Ticket"":{""TaskId"":""F12""},""LastLogSequence"":12}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(CancelScriptCommand));

            var cancelScriptCommand = hasAThing.TheThing as CancelScriptCommand;
            cancelScriptCommand.Ticket.TaskId.Should().Be("F12");
            cancelScriptCommand.LastLogSequence.Should().Be(12L);
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_CompleteScriptCommand()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.CompleteScriptCommand, Octopus.Shared"",""Ticket"":{""TaskId"":""F12""},""LastLogSequence"":12}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(CompleteScriptCommand));

            var completeScriptCommand = hasAThing.TheThing as CompleteScriptCommand;
            completeScriptCommand.Ticket.TaskId.Should().Be("F12");
            completeScriptCommand.LastLogSequence.Should().Be(12L);
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_ScriptFile()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.ScriptFile, Octopus.Shared"",""Name"":""Alice"",""Contents"":{""id"":""06dfdd58-b843-431f-9521-fae3b72b9bcd"",""length"":3},""EncryptionPassword"":null}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(ScriptFile));

            var scriptFile = hasAThing.TheThing as ScriptFile;
            scriptFile.Name.Should().Be("Alice");
            // No need to check the DataStream, that is handled in a special way in halibut.
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_ScriptStatusRequest()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.ScriptStatusRequest, Octopus.Shared"",""Ticket"":{""TaskId"":""ticket""},""LastLogSequence"":1337}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(ScriptStatusRequest));

            var scriptStatusRequest = hasAThing.TheThing as ScriptStatusRequest;
            scriptStatusRequest.Ticket.TaskId.Should().Be("ticket");
            scriptStatusRequest.LastLogSequence.Should().Be(1337L);
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_ScriptStatusResponse()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.ScriptStatusResponse, Octopus.Shared"",""Ticket"":{""TaskId"":""foo""},""Logs"":[{""Source"":0,""Occurred"":""2023-04-21T03:51:31.7359531+00:00"",""Text"":""something""}],""NextLogSequence"":555,""State"":0,""ExitCode"":12}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(ScriptStatusResponse));

            var scriptStatusResponse = hasAThing.TheThing as ScriptStatusResponse;
            scriptStatusResponse.Ticket.TaskId.Should().Be("foo");
            scriptStatusResponse.State.Should().Be(ProcessState.Pending);
            scriptStatusResponse.Logs.Count.Should().Be(1);
            scriptStatusResponse.Logs[0].Text.Should().Be("something");
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_StartScriptCommand()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.StartScriptCommand, Octopus.Shared"",""ScriptBody"":""echo hello"",""Isolation"":1,""Scripts"":{""Bash"":""bob""},""Files"":[],""Arguments"":[""arg1"",""arg2""],""TaskId"":""servertask-12"",""ScriptIsolationMutexTimeout"":""00:01:00"",""IsolationMutexName"":""mutex""}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(StartScriptCommand));

            var scriptStatusResponse = hasAThing.TheThing as StartScriptCommand;
            scriptStatusResponse.ScriptBody.Should().Be("echo hello");
            scriptStatusResponse.Isolation.Should().Be(ScriptIsolationLevel.FullIsolation);
            scriptStatusResponse.Scripts.Count.Should().Be(1);
            scriptStatusResponse.Scripts[ScriptType.Bash].Should().Be("bob");
            scriptStatusResponse.IsolationMutexName.Should().Be("mutex");
        }

        [Test]
        public void BackwardsCompatabilityTest_CanDeserializeJson_WithLegacy_UploadResult()
        {
            var json = @"{""TheThing"":{""$type"":""Octopus.Shared.Contracts.UploadResult, Octopus.Shared"",""FullPath"":""/the/path"",""Hash"":""thehash"",""Length"":1337}}";

            var serializer = CreateJsonSerializer();

            var hasAThing = serializer.FromJson<HasAThing>(json);
            hasAThing.TheThing.GetType().Should().Be(typeof(UploadResult));

            var scriptStatusResponse = hasAThing.TheThing as UploadResult;
            scriptStatusResponse.FullPath.Should().Be("/the/path");
        }

        private static JsonSerializer CreateJsonSerializer()
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
            };

            MessageSerializerBuilderExtensionMethods.AddLegacyContractSupportToJsonSerializer(jsonSerializerSettings);
            var serializer = JsonSerializer.Create(jsonSerializerSettings);
            return serializer;
        }
    }

    public class HasAThing
    {
        public HasAThing(object theThing)
        {
            TheThing = theThing;
        }

        public object TheThing { get; }
    }
}