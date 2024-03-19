using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Scripts
{
    public delegate Task OnScriptCompleted(CancellationToken cancellationToken);
}