﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Services.Scripts
{
    public interface IAsyncScriptServiceV2
    {
        Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, CancellationToken cancellationToken);
        Task CompleteScriptAsync(CompleteScriptCommandV2 command, CancellationToken cancellationToken);
    }
}