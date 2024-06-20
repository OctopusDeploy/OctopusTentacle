using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client
{
    public interface IEventDrivenScriptExecutor
    {
        Task<(ScriptStatus, ITicketForNextStatus)> StartScript(ExecuteScriptCommand executeScriptCommand,
            HasStartScriptBeenCalledBefore hasStartScriptBeenCalledBefore,
            CancellationToken cancellationToken);
        
        Task<(ScriptStatus, ITicketForNextStatus)> GetStatus(ITicketForNextStatus ticketForNextNextStatus, CancellationToken cancellationToken);
        
        /// <summary>
        /// Cancel script will still send back the rest of the logs, hence the ticketForNextNextStatus argument.
        /// </summary>
        /// <param name="ticketForNextNextStatus"></param>
        /// <param name="hasStartScriptBeenCalledBefore"></param>
        /// <returns></returns>
        Task<(ScriptStatus, ITicketForNextStatus)> CancelScript(ITicketForNextStatus ticketForNextNextStatus, CancellationToken cancellationToken);
        
        /// <summary>
        /// Use this cancel method if only the ScriptTicket is known, e.g. we called StartScript but never got a response.
        /// </summary>
        /// <param name="scriptTicket"></param>
        /// <param name="hasStartScriptBeenCalledBefore"></param>
        /// <returns></returns>
        Task<(ScriptStatus, ITicketForNextStatus)> CancelScript(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        
        Task CleanUpScript(ScriptTicket scriptTicket, CancellationToken cancellationToken);
    }

    public enum HasStartScriptBeenCalledBefore
    {
        NoNever,
        ItMayHaveBeen
    }
}