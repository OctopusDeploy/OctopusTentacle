using System;
using System.Data.SqlClient;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Startup
{
    public class DeleteInstanceCommand : AbstractStandardCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        readonly ILog log;

        public DeleteInstanceCommand(IApplicationInstanceSelector instanceSelector, ILog log): base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            this.log = log;
        }

        protected override void Start()
        {
            var currentInstance = instanceSelector.GetCurrentInstance();
            RemoveNodeFromDatabase(currentInstance);

            instanceSelector.DeleteInstance();
        }

        private void RemoveNodeFromDatabase(LoadedApplicationInstance instance)
        {
            var connectionString = instance.Configuration.Get<string>("Octopus.Storage.ExternalDatabaseConnectionString");
            var serverName = instance.Configuration.Get<string>("Octopus.Server.NodeName");

            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(serverName))
                return;

            using (var connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    const string commandText = "DELETE FROM OctopusServerNode WHERE [Id] = @serverName";
                    using (var cmd = new SqlCommand(commandText, connection))
                    {
                        cmd.Parameters.AddWithValue("serverName", serverName);
                        cmd.ExecuteNonQuery();
                    }
                    log.Info($"Deregistered {instance.InstanceName} from the database");
                }
                catch (Exception)
                {                    
                    log.Warn("Could not open the database. This instance has not been deregistered.");
                }
            }            
        }
    }
}