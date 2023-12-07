using System;
using System.Threading.Tasks;
using Halibut;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class ExceptionContractAssertionBuilder
    {
        readonly FailureScenario failureScenario;
        readonly TentacleType tentacleType;
        readonly ClientAndTentacle clientAndTentacle;

        public ExceptionContractAssertionBuilder(FailureScenario failureScenario, TentacleType tentacleType, ClientAndTentacle clientAndTentacle)
        {
            this.failureScenario = failureScenario;
            this.tentacleType = tentacleType;
            this.clientAndTentacle = clientAndTentacle;
        }
        
        public ExceptionContract Build()
        {
            if (failureScenario == FailureScenario.ConnectionFaulted)
            {
                switch (tentacleType)
                {
                    case TentacleType.Listening:
                        return new ExceptionContract(typeof(HalibutClientException), new[]
                        {
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Transport endpoint is not connected",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Connection refused",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Broken pipe",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin:  Received an unexpected EOF or 0 bytes from the transport stream",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Unable to write data to the transport connection: Broken pipe",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Unable to read data from the transport connection: Broken pipe",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Attempted to read past the end of the stream.",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: An established connection was aborted by the software in your host machine",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Unable to write data to the transport connection: An established connection was aborted by the software in your host machine",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Unable to read data from the transport connection: An established connection was aborted by the software in your host machine",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: An existing connection was forcibly closed by the remote host",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Unable to write data to the transport connection: An existing connection was forcibly closed by the remote host",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', before the request could begin: Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host",
                            
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Broken pipe",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began:  Received an unexpected EOF or 0 bytes from the transport stream",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Unable to write data to the transport connection: Broken pipe",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Unable to read data from the transport connection: Broken pipe",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Attempted to read past the end of the stream.",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: An established connection was aborted by the software in your host machine",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Unable to write data to the transport connection: An established connection was aborted by the software in your host machine",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Unable to read data from the transport connection: An established connection was aborted by the software in your host machine",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: An existing connection was forcibly closed by the remote host",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Unable to write data to the transport connection: An existing connection was forcibly closed by the remote host",
                            $"An error occurred when sending a request to '{clientAndTentacle.ServiceEndPoint}/', after the request began: Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host"
                        });
                    case TentacleType.Polling:
                        return new ExceptionContract(typeof(HalibutClientException), new[]
                        {
                            "Transport endpoint is not connected",
                            "Connection refused",
                            "Broken pipe",
                            "Received an unexpected EOF or 0 bytes from the transport stream",
                            "Unable to write data to the transport connection: Broken pipe",
                            "Unable to read data from the transport connection: Broken pipe",
                            "Attempted to read past the end of the stream.",
                            "Unable to write data to the transport connection: An established connection was aborted by the software in your host machine",
                            "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine",
                            "Unable to write data to the transport connection: An existing connection was forcibly closed by the remote host",
                            "Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host"
                        });
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (failureScenario == FailureScenario.ScriptExecutionCancelled)
            {
                return new ExceptionContract(
                    new[]{
                        typeof(OperationCanceledException),
                        typeof(TaskCanceledException)
                    },
                    new[]
                    {
                        "Script execution was cancelled",
                        "A task was canceled.", // Cancellation during connection can throw this error
                        "The operation was cancelled" // Cancellation during connection can throw this error
                    });
            }

            throw new NotSupportedException("Scenario not supported");
        }
    }
}