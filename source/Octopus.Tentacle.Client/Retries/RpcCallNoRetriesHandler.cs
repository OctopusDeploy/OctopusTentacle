using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Retries
{
    public class RpcCallNoRetriesHandler
    {
        public async Task ExecuteWithNoRetries(
            Func<CancellationToken, Task> action,
            bool abandonActionOnCancellation,
            TimeSpan abandonAfter,
            CancellationToken cancellationToken)
        {
            if (!abandonActionOnCancellation)
            {
                await action(cancellationToken).ConfigureAwait(false);
                return;
            }

            var actionTask = action(cancellationToken);

            var actionTaskCompletionResult = await actionTask.WaitTillCompletion(abandonAfter, cancellationToken);
            if (actionTaskCompletionResult == TaskCompletionResult.Abandoned)
            {
                throw new OperationAbandonedException(abandonAfter);
            }


            await actionTask.ConfigureAwait(false);
        }

        public async Task<T> ExecuteWithNoRetries<T>(
            Func<CancellationToken, Task<T>> action,
            bool abandonActionOnCancellation,
            TimeSpan abandonAfter,
            CancellationToken cancellationToken)
        {
            if (!abandonActionOnCancellation)
            {
                return await action(cancellationToken).ConfigureAwait(false);
            }

            var actionTask = action(cancellationToken);

            var actionTaskCompletionResult = await actionTask.WaitTillCompletion(abandonAfter, cancellationToken);
            if (actionTaskCompletionResult == TaskCompletionResult.Abandoned)
            {
                throw new OperationAbandonedException(abandonAfter);
            }

            return await actionTask.ConfigureAwait(false);
        }
    }
}
