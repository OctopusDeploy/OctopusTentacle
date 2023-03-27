extern alias TaskScheduler;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using NUnit.Framework;

using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace Octopus.Tentacle.Tests.Integration
{
    public class WorksWithOctopusFixture
    {
        [Test]
        public async Task Foo()
        {
            string weatherForecastStorage = "weatherForecastStorage";

            string connectionString = $"server={weatherForecastStorage};user id={MsSqlBuilder.DefaultUsername};password={MsSqlBuilder.DefaultPassword};database={MsSqlBuilder.DefaultDatabase}";

            
            // var _weatherForecastNetwork = new NetworkBuilder()
            //     .Build();
            
            var _msSqlContainer = new MsSqlBuilder()
                //.WithNetwork(_weatherForecastNetwork)
                //.WithNetworkAliases(weatherForecastStorage)
                .WithPortBinding(1255, 1433)
                .Build();

            //await _weatherForecastNetwork.CreateAsync();

            await _msSqlContainer.StartAsync();

            try
            {
                while ("dfdf".Length == -1)
                {
                    Thread.Sleep(1000);
                }
            }
            finally
            {
                await _msSqlContainer.DisposeAsync();
                //await _weatherForecastNetwork.DeleteAsync();
            }

        }
        
        // private readonly TestcontainerDatabase testcontainers = new TestcontainersBuilder<PostgreSqlTestcontainer>()
        //     .WithDatabase(new PostgreSqlTestcontainerConfiguration
        //     {
        //         Database = "db",
        //         Username = "postgres",
        //         Password = "postgres",
        //     })
        //     .Build();
        
        [Test]
        public async Task ThisOne()
        {
            await Task.CompletedTask;
        }
        
    }
    
    
}