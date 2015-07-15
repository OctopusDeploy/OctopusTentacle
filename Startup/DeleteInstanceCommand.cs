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
            var isDefault = false;
            if (string.IsNullOrWhiteSpace(instanceName))
            {
                isDefault = true;
                instanceSelector.LoadDefaultInstance();
                instanceName = instanceSelector.Current.InstanceName;
            }
            else
            {
                instanceSelector.LoadInstance(instanceName);
            }
            var connectionString = instanceSelector.Current.Configuration.Get("Octopus.Storage.ExternalDatabaseConnectionString");
            var serverName = instanceSelector.Current.Configuration.Get("Octopus.Server.NodeName");
            
            if (!string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(serverName))
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
                log.InfoFormat("Deregistered {0} from the database", instanceName);
            }

            if (isDefault)
            {
                instanceSelector.DeleteDefaultInstance();
            }
            else
            {
                instanceSelector.DeleteInstance(instanceName);
            }
        }

    }
}