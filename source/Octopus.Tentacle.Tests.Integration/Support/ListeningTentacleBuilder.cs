using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Client;
using Octopus.Client.Model;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ListeningTentacleBuilder
    {
        //ITentacleExecutionContext tentacleExecutionContext;

        //public ListeningTentacleBuilder(ITentacleExecutionContext tentacleExecutionContext)
        //{
        //    this.tentacleExecutionContext = tentacleExecutionContext;
        //}

        public async Task<Tentacle> Build(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            return new Tentacle(new Uri("http://localhost:1234"));

            //PollingTentacleInstance instance = await PollingTentacleInstance.Create(tentacleExecutionContext,
            //    spaceContext.Test.Log,
            //    spaceContext.Repository,
            //    serverTentacleConnectionDetails,
            //    TentacleInstanceRunnerType.Console, // Always run as a console in xUnit since a service could leave more things hanging around.
            //    Some.ShortRandomName());
            //var tentacle = await instance.RegisterWithOctopusServer(environment: environment, roles: roles, space: spaceContext.Space.Name);
            //return (tentacle, new RealTentacleDisposer(instance));
        }
    }

    public class Tentacle : IAsyncDisposable
    {
        public Tentacle(Uri tentacleUri)
        {
            TentacleUri = tentacleUri;
        }

        public Uri TentacleUri { get; }

        public async ValueTask DisposeAsync()
        {
            await Task.CompletedTask;
        }
    }

    //public class SomeTentacleExecutionContext : ITentacleExecutionContext
    //{
    //    public SomeTentacleExecutionContext(string tempDir, string tentacleExePath)
    //    {
    //        TempDir = tempDir;
    //        TentacleExePath = tentacleExePath;
    //    }

    //    public string TempDir { get; }
    //    public string TentacleExePath { get; }


    //}
}