using System;
using System.ComponentModel;
using NLog;
using NLog.Layouts;
using NLog.Targets;

namespace Octopus.Tentacle.Diagnostics
{
    /// <summary>
    /// Acts as a /dev/null for logs. Added to .nlog config files to still refer to the `EventLog` type for the sake of windows-based instances
    /// while just consuming them on linux
    /// </summary>
    public class NullLogTarget : TargetWithLayout
    {
        /// <summary>
        /// Gets or sets a value indicating whether to perform layout calculation.
        /// </summary>
        /// <docgen category="Layout Options" order="10" />
        [DefaultValue(false)]
        public bool FormatMessage { get; set; }

        /// <summary>
        /// Gets or sets the value to be used as the event Source.
        /// </summary>
        /// <remarks>
        /// By default this is the friendly name of the current AppDomain.
        /// </remarks>
        /// <docgen category='Event Log Options' order='10' />
        public Layout? Source { get; set; }

        /// <summary>
        /// Does nothing. Optionally it calculates the layout text but
        /// discards the results.
        /// </summary>
        /// <param name="logEvent">The logging event.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            if (!FormatMessage)
                return;
            Layout.Render(logEvent);
        }
    }
}