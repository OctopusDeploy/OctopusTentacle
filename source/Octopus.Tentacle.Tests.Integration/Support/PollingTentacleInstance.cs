// using System;
// using System.Collections.Generic;
// using System.Text;
// using System.Threading.Tasks;
// using System.Xml.Linq;
// using FluentAssertions;
// using Halibut;
// using Octopus.Client;
// using Octopus.Client.Extensions;
// using Octopus.Client.Model;
// using Octopus.Diagnostics;
// using Octopus.E2ETests.Universe.Proxies;
// using Octopus.E2ETests.Universe.Setup;
// using Octopus.Shared.Util;
// using Octopus.Tentacle.Tests.Integration.Support;
// using Octopus.Tests.Common;
// using Serilog;
//
// namespace Octopus.E2ETests.Universe.Machines
// {
//     public class PollingTentacleInstance // : TentacleInstance // TODO luke
//     {
//         public enum PollingProtocol
//         {
//             Tcp,
//             WebSocket
//         }
//
//         readonly ServerTentacleConnectionDetails serverTentacleConnectionDetails;
//
//         PollingTentacleInstance(ITentacleExecutionContext testExecutionContext,
//             ILog log,
//             IOctopusAsyncRepository repository,
//             ServerTentacleConnectionDetails serverTentacleConnectionDetails,
//             string name)
//         {
//             this.serverTentacleConnectionDetails = serverTentacleConnectionDetails;
//         }
//
//         public static async Task<PollingTentacleInstance> Create(
//             E2ETestExecutionContextWithLocalExecutables e2ETestExecutionContextWithLocalExecutables,
//             ILogger log,
//             IOctopusApiClient client,
//             string instanceName)
//         {
//             var tentacleInstanceRunnerType = OperatingSystem.IsWindows() ? TentacleInstanceRunnerType.WindowsService : TentacleInstanceRunnerType.Console;
//             return await Create(new E2ETentacleExecutionContext(e2ETestExecutionContextWithLocalExecutables),
//                 log,
//                 client.Repository,
//                 ServerTentacleConnectionDetails.From(client.Server),
//                 tentacleInstanceRunnerType,
//                 instanceName);
//         }
//
//         public static async Task<PollingTentacleInstance> Create(
//             ITentacleExecutionContext testExecutionContext,
//             ILogger log,
//             IOctopusAsyncRepository repository,
//             ServerTentacleConnectionDetails serverTentacleConnectionDetails,
//             TentacleInstanceRunnerType tentacleInstanceRunnerType,
//             string instanceName)
//         {
//             var instance = new PollingTentacleInstance(testExecutionContext, log, repository, serverTentacleConnectionDetails, tentacleInstanceRunnerType, instanceName);
//             await instance.Initialize();
//             return instance;
//         }
//
//         public async Task<PollingTentacleInstance> UsingProxy(IProxyServerConfiguration proxyConfig)
//         {
//             await ConfigureProxy(proxyConfig);
//             await Restart();
//             return this;
//         }
//
//         public async Task<MachineResource> RegisterWithOctopusServer(
//             string? name = null,
//             EnvironmentResource? environment = null,
//             IReadOnlyCollection<string>? roles = null,
//             PollingProtocol protocol = PollingProtocol.Tcp,
//             bool startTentacle = true,
//             string? space = null)
//         {
//             if (protocol == PollingProtocol.WebSocket && string.IsNullOrWhiteSpace(serverTentacleConnectionDetails.WebSocketUrl))
//             {
//                 if (HalibutRuntime.OSSupportsWebSockets)
//                 {
//                     throw new Exception($"Web sockets is not set up on the target Octopus Server even though the current Operating System ({RuntimeInformationHelper.OSDescription}) supports web sockets? The results of this test would be invalidated if we don't use web sockets.");
//                 }
//
//                 Log.Warning(
//                     "Web sockets is not supported on the current Operating System ({OperatingSystem}). Falling back to a TCP connection for polling.",
//                     RuntimeInformationHelper.OSDescription);
//                 protocol = PollingProtocol.Tcp;
//             }
//
//             // Fall back to the instance name if no custom name is specified
//             var nameToUse = name ?? Name;
//
//             // Create a solo environment if no environment is specified
//             var environmentToUse = environment
//                 ?? (await Repository.Environments.CreateOrModify(nameToUse, $"Solo environment for {nameToUse}")).Instance;
//
//             // Fall back to a solo role if no roles are specified
//             var rolesToUse = roles?.CommaSeparate() ?? nameToUse;
//
//             var register = new StringBuilder()
//                 .Append($"--apiKey=\"{serverTentacleConnectionDetails.ApiKey}\"")
//                 .Append($" --server=\"{serverTentacleConnectionDetails.HttpListenUri}\"")
//                 .Append($" --name=\"{nameToUse}\"")
//                 .Append(" --comms-style=\"TentacleActive\"")
//                 .Append(protocol == PollingProtocol.Tcp ? $" --server-comms-port=\"{serverTentacleConnectionDetails.CommsPort}\"" : $" --server-web-socket=\"{serverTentacleConnectionDetails.WebSocketUrl}\"")
//                 .Append($" --environment=\"{environmentToUse.Name}\"")
//                 .Append($" --role=\"{rolesToUse}\"");
//             if (space != null) register = register.Append($" --space=\"{space}\"");
//             await serverRunner.RunCommand("register-with", register.ToString());
//
//             var machine = await Repository.Machines.FindByName(nameToUse);
//             machine.Should().NotBeNull($"we expected to find the Tentacle we just registered with the name '{nameToUse}'");
//             machine.Thumbprint.Should().Be(CertificateThumbprint, "we expected the Tentacle we just registered to have the same thumbprint");
//
//             MachinesWeRegistered.Add(machine);
//             await WaitForMachineToBeHealthy(machine);
//             // Start the Tentacle after it is registered and has a mailbox to poll!
//             if (startTentacle)
//             {
//                 await Start();
//             }
//
//             return machine;
//         }
//
//         public async Task<WorkerResource> RegisterAsWorkerWithOctopusServer(
//             string? name = null,
//             WorkerPoolResource? pool = null,
//             PollingProtocol protocol = PollingProtocol.Tcp,
//             bool startTentacle = true)
//         {
//             if (protocol == PollingProtocol.WebSocket && string.IsNullOrWhiteSpace(serverTentacleConnectionDetails.WebSocketUrl))
//             {
//                 if (HalibutRuntime.OSSupportsWebSockets)
//                 {
//                     throw new Exception($"Web sockets is not set up on the target Octopus Server even though the current Operating System ({RuntimeInformationHelper.OSDescription}) supports web sockets? The results of this test would be invalidated if we don't use web sockets.");
//                 }
//
//                 Log.Warning(
//                     "Web sockets is not supported on the current Operating System ({OperatingSystem}). Falling back to a TCP connection for polling.",
//                     RuntimeInformationHelper.OSDescription);
//                 protocol = PollingProtocol.Tcp;
//             }
//
//             // Fall back to the instance name if no custom name is specified
//             var nameToUse = name ?? Name;
//
//             // use default pool if none is given
//             var poolToUse = pool ?? await Repository.WorkerPools.FindOne(wp => wp.IsDefault);
//
//             var register = new StringBuilder()
//                 .Append($" --apiKey=\"{serverTentacleConnectionDetails.ApiKey}\"")
//                 .Append($" --server=\"{serverTentacleConnectionDetails.HttpListenUri}\"")
//                 .Append($" --name=\"{nameToUse}\"")
//                 .Append(" --comms-style=\"TentacleActive\"")
//                 .Append(protocol == PollingProtocol.Tcp ? $" --server-comms-port=\"{serverTentacleConnectionDetails.CommsPort}\"" : $" --server-web-socket=\"{serverTentacleConnectionDetails.WebSocketUrl}\"")
//                 .Append($" --workerpool=\"{poolToUse.Name}\"");
//             await serverRunner.RunCommand("register-worker", register.ToString());
//
//             var worker = await Repository.Workers.FindByName(nameToUse);
//             worker.Should().NotBeNull($"we expected to find the Tentacle we just registered with the name '{nameToUse}'");
//             worker.Thumbprint.Should().Be(CertificateThumbprint, "we expected the Tentacle we just registered to have the same thumbprint");
//
//             WorkersWeRegistered.Add(worker);
//             await WaitForWorkerToBeHealthy(worker);
//
//             // Start the Tentacle after it is registered and has a mailbox to poll!
//             if (startTentacle)
//             {
//                 await Start();
//             }
//
//             Log.Information("Registered {Tentacle} in worker pool {WorkerPool} ({Id})", nameToUse, poolToUse.Name, poolToUse.Id);
//
//             return worker;
//         }
//
//         class PollingTentacleWindowsServiceRunner : BaseTentacleWindowsServiceRunner
//         {
//             readonly PollingTentacleInstance instance;
//
//             public PollingTentacleWindowsServiceRunner(PollingTentacleInstance instance, string instanceName, ILogger log, ITentacleExecutionContext testExecutionContext) : base(instanceName, log, testExecutionContext)
//             {
//                 this.instance = instance;
//             }
//
//             public override async Task Install()
//             {
//                 await RunCommand("new-certificate");
//                 var configuration = new StringBuilder();
//                 configuration.Append($" --app=\"{instance.ApplicationsDirectory}\"");
//                 // This is a Polling Tentacle
//                 configuration.Append(" --noListen=true --port=0");
//                 await RunCommand("configure", configuration.ToString());
//                 await base.Install();
//             }
//         }
//
//         class PollingTentacleConsoleRunner : TentacleConsoleRunner, IServerInstaller
//         {
//             readonly PollingTentacleInstance instance;
//
//             public PollingTentacleConsoleRunner(PollingTentacleInstance instance, string instanceName, ILogger log, ITentacleExecutionContext testExecutionContext) : base(instanceName, log, testExecutionContext)
//             {
//                 this.instance = instance;
//             }
//
//             protected override string ActuallyStartedMessage => "Agent will not listen";
//
//             public async Task Install()
//             {
//                 await RunCommand("new-certificate");
//                 var configuration = new StringBuilder();
//                 configuration.Append($"--app=\"{instance.ApplicationsDirectory}\"");
//                 // This is a Polling Tentacle
//                 configuration.Append(" --noListen=true --port=0");
//                 await RunCommand("configure", configuration.ToString());
//             }
//
//             public async Task UnInstall()
//             {
//                 await Task.CompletedTask;
//             }
//         }
//     }
// }