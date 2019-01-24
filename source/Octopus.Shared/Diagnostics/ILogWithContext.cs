using System;
using Octopus.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public interface ILogWithContext : ILog
    {
        LogContext CurrentContext { get; }
        bool IsVerboseEnabled { get; }
        bool IsErrorEnabled { get; }
        bool IsFatalEnabled { get; }
        bool IsInfoEnabled { get; }
        bool IsTraceEnabled { get; }
        bool IsWarnEnabled { get; }

        /// <summary>
        /// Opens a new child block for logging.
        /// </summary>
        /// <param name="messageText">Title of the new block.</param>
        /// <returns>An <see cref="IDisposable" /> that will automatically revert the current block when disposed.</returns>
        IDisposable OpenBlock(string messageText);

        /// <summary>
        /// Opens a new child block for logging.
        /// </summary>
        /// <param name="messageFormat">Format string for the message.</param>
        /// <param name="args">Arguments for the format string.</param>
        /// <returns>An <see cref="IDisposable" /> that will automatically revert the current block when disposed.</returns>
        [StringFormatMethod("messageFormat")]
        IDisposable OpenBlock(string messageFormat, params object[] args);

        /// <summary>
        /// Plans a new block of output that will be used in the future for grouping child blocks for logging.
        /// </summary>
        /// <param name="messageText">Title of the new block.</param>
        /// <returns>An <see cref="LogContext" /> that will automatically revert the current block when disposed.</returns>
        LogContext PlanGroupedBlock(string messageText);

        /// <summary>
        /// Plans a new block of log output that will be used in the future. This is typically used for high-level log
        /// information, such as the steps in a big deployment process.
        /// </summary>
        /// <param name="messageText">Title of the new block.</param>
        /// <returns>An <see cref="IDisposable" /> that will automatically revert the current block when disposed.</returns>
        LogContext PlanFutureBlock(string messageText);

        /// <summary>
        /// Plans a new block of log output that will be used in the future. This is typically used for high-level log
        /// information, such as the steps in a big deployment process.
        /// </summary>
        /// <param name="messageFormat">Format string for the message.</param>
        /// <param name="args">Arguments for the format string.</param>
        /// <returns>An <see cref="IDisposable" /> that will automatically revert the current block when disposed.</returns>
        [StringFormatMethod("messageFormat")]
        LogContext PlanFutureBlock(string messageFormat, params object[] args);

        /// <summary>
        /// Switches to a new logging context on the current thread, allowing you to begin logging within a block previously
        /// begun using <see cref="o:OpenBlock" /> or <see cref="o:PlanFutureBlock" />.
        /// </summary>
        /// <param name="logContext">The <see cref="LogContext" /> to swIKeyValueStoreitch to.</param>
        /// <returns>An <see cref="IDisposable" /> that will automatically revert the current block when disposed.</returns>
        IDisposable WithinBlock(LogContext logContext);

        /// <summary>
        /// Marks the current block as abandoned. Abandoned blocks won't be shown in the task log.
        /// </summary>
        void Abandon();

        /// <summary>
        /// If a block was previously abandoned using <see cref="Abandon" />, calling <see cref="Reinstate" /> will un-abandon
        /// it.
        /// </summary>
        void Reinstate();

        /// <summary>
        /// Marks the current block as finished. If there were any errors, it will be finished with errors. If there were no
        /// errors, it will be assumed to be successful.
        /// </summary>
        void Finish();

        bool IsEnabled(LogCategory category);

        void UpdateProgress(int progressPercentage, string messageText);

        [StringFormatMethod("messageFormat")]
        void UpdateProgressFormat(int progressPercentage, string messageFormat, params object[] args);
    }
}