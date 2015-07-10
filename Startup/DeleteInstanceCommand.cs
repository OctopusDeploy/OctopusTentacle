using System;
using System.Data.SqlClient;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class DeleteInstanceCommand : AbstractCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        readonly ILog log;
        string instanceName;

        public DeleteInstanceCommand(IApplicationInstanceSelector instanceSelector, ILog log)
        {
            this.instanceSelector = instanceSelector;
            this.log = log;
            Options.Add("instance=", "Name of the instance to delete", v => instanceName = v);
        }

        protected override void Start()
        {
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                instanceSelector.LoadDefaultInstance();
                var connectionString = instanceSelector.Current.Configuration.Get("Octopus.Storage.ExternalDatabaseConnectionString");
                var defaultInstanceName = instanceSelector.Current.InstanceName;
                instanceSelector.DeleteDefaultInstance();
                DeregisterInstance(connectionString, defaultInstanceName);
            }
            else
            {
                instanceSelector.LoadInstance(instanceName);
                var connectionString = instanceSelector.Current.Configuration.Get("Octopus.Storage.ExternalDatabaseConnectionString");
                instanceSelector.DeleteInstance(instanceName);
                DeregisterInstance(connectionString, instanceName);
            }
        }

        void DeregisterInstance(string connectionString, string serverName)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                const string commandText = "DELETE FROM OctopusServerNode WHERE [Id] = @serverName";
                using (var cmd = new SqlCommand(commandText, connection))
                {
                    cmd.Parameters.AddWithValue("serverName", serverName);
                    cmd.ExecuteNonQuery();
                }
            }
            log.InfoFormat("Deregistered {0} from the database", serverName);
        }

    }
}