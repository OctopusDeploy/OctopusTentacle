#nullable disable
#if !NLOG_HAS_EVENT_LOG_TARGET
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Internal.Fakeables;
using NLog.Layouts;
using NLog.Targets;

namespace Octopus.Tentacle.Diagnostics
{
    /// <summary>
    /// Action that should be taken if the message is greater than
    /// the max message size allowed by the Event Log.
    /// </summary>
    public enum EventLogTargetOverflowAction
    {
        /// <summary>
        /// Truncate the message before writing to the Event Log.
        /// </summary>
        Truncate,

        /// <summary>
        /// Split the message and write multiple entries to the Event Log.
        /// </summary>
        Split,

        /// <summary>
        /// Discard of the message. It will not be written to the Event Log.
        /// </summary>
        Discard
    }

    /// <summary>
    /// Writes log message to the Event Log.
    /// </summary>
    /// <seealso href="https://github.com/nlog/nlog/wiki/EventLog-target">Documentation on NLog Wiki</seealso>
    /// <example>
    ///     <p>
    /// To set up the target in the <a href="config.html">configuration file</a>,
    /// use the following syntax:
    ///     </p>
    ///     <code lang="XML" source="examples/targets/Configuration File/EventLog/NLog.config" />
    ///     <p>
    /// This assumes just one target and a single rule. More configuration
    /// options are described <a href="config.html">here</a>.
    ///     </p>
    ///     <p>
    /// To set up the log target programmatically use code like this:
    ///     </p>
    ///     <code lang="C#" source="examples/targets/Configuration API/EventLog/Simple/Example.cs" />
    /// </example>
    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public class EventLogTarget : TargetWithLayout, IInstallable
    {
        EventLog eventLogInstance;

        int maxMessageLength;

        long? maxKilobytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogTarget" /> class.
        /// </summary>
        public EventLogTarget()
#pragma warning disable CS0618
            : this(LogFactory.CurrentAppDomain)
#pragma warning restore CS0618
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogTarget" /> class.
        /// </summary>
#pragma warning disable CS0618
        public EventLogTarget(IAppDomain appDomain)
#pragma warning restore CS0618
        {
            Source = appDomain.FriendlyName;
            Log = "Application";
            MachineName = ".";
            MaxMessageLength = 16384;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventLogTarget" /> class.
        /// </summary>
        /// <param name="name">Name of the target.</param>
        public EventLogTarget(string name)
#pragma warning disable CS0618
            : this(LogFactory.CurrentAppDomain)
#pragma warning restore CS0618
        {
            Name = name;
        }

        /// <summary>
        /// Gets or sets the name of the machine on which Event Log service is running.
        /// </summary>
        /// <docgen category='Event Log Options' order='10' />
        [DefaultValue(".")]
        public string MachineName { get; set; }

        /// <summary>
        /// Gets or sets the layout that renders event ID.
        /// </summary>
        /// <docgen category='Event Log Options' order='10' />
        public Layout EventId { get; set; }

        /// <summary>
        /// Gets or sets the layout that renders event Category.
        /// </summary>
        /// <docgen category='Event Log Options' order='10' />
        public Layout Category { get; set; }

        /// <summary>
        /// Optional entrytype. When not set, or when not convertable to <see cref="EventLogEntryType" /> then determined by <see cref="NLog.LogLevel" />
        /// </summary>
        public Layout EntryType { get; set; }

        /// <summary>
        /// Gets or sets the value to be used as the event Source.
        /// </summary>
        /// <remarks>
        /// By default this is the friendly name of the current AppDomain.
        /// </remarks>
        /// <docgen category='Event Log Options' order='10' />
        public Layout Source { get; set; }

        /// <summary>
        /// Gets or sets the name of the Event Log to write to. This can be System, Application or
        /// any user-defined name.
        /// </summary>
        /// <docgen category='Event Log Options' order='10' />
        [DefaultValue("Application")]
        public string Log { get; set; }

        /// <summary>
        /// Gets or sets the message length limit to write to the Event Log.
        /// </summary>
        /// <remarks>
        /// <value>MaxMessageLength</value>
        /// cannot be zero or negative</remarks>
        [DefaultValue(16384)]
        public int MaxMessageLength
        {
            get => maxMessageLength;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("MaxMessageLength cannot be zero or negative.");

                maxMessageLength = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum Event log size in kilobytes.
        /// 
        /// If <c>null</c>, the value won't be set.
        /// 
        /// Default is 512 Kilobytes as specified by Eventlog API
        /// </summary>
        /// <remarks>
        /// <value>MaxKilobytes</value>
        /// cannot be less than 64 or greater than 4194240 or not a multiple of 64. If <c>null</c>, use the default value</remarks>
        [DefaultValue(null)]
        public long? MaxKilobytes
        {
            get => maxKilobytes;
            set
            {
                //Event log API restriction
                if (value != null && (value < 64 || value > 4194240 || value % 64 != 0))
                    throw new ArgumentException("MaxKilobytes must be a multitude of 64, and between 64 and 4194240");
                maxKilobytes = value;
            }
        }

        /// <summary>
        /// Gets or sets the action to take if the message is larger than the <see cref="MaxMessageLength" /> option.
        /// </summary>
        /// <docgen category='Event Log Overflow Action' order='10' />
        [DefaultValue(EventLogTargetOverflowAction.Truncate)]
        public EventLogTargetOverflowAction OnOverflow { get; set; }

        /// <summary>
        /// Performs installation which requires administrative permissions.
        /// </summary>
        /// <param name="installationContext">The installation context.</param>
        public void Install(InstallationContext installationContext)
        {
            var fixedSource = GetFixedSource();

            //always throw error to keep backwardscomp behavior.
            CreateEventSourceIfNeeded(fixedSource, true);
        }

        /// <summary>
        /// Performs uninstallation which requires administrative permissions.
        /// </summary>
        /// <param name="installationContext">The installation context.</param>
        public void Uninstall(InstallationContext installationContext)
        {
            var fixedSource = GetFixedSource();

            if (string.IsNullOrEmpty(fixedSource))
                InternalLogger.Debug("Skipping removing of event source because it contains layout renderers");
            else
                EventLog.DeleteEventSource(fixedSource, MachineName);
        }

        /// <summary>
        /// Determines whether the item is installed.
        /// </summary>
        /// <param name="installationContext">The installation context.</param>
        /// <returns>
        /// Value indicating whether the item is installed or null if it is not possible to determine.
        /// </returns>
        public bool? IsInstalled(InstallationContext installationContext)
        {
            var fixedSource = GetFixedSource();

            if (!string.IsNullOrEmpty(fixedSource))
                return EventLog.SourceExists(fixedSource, MachineName);

            InternalLogger.Debug("Unclear if event source exists because it contains layout renderers");
            return null; //unclear!
        }

        /// <summary>
        /// Initializes the target.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var fixedSource = GetFixedSource();

            if (string.IsNullOrEmpty(fixedSource))
            {
                InternalLogger.Debug("Skipping creation of event source because it contains layout renderers");
            }
            else
            {
                var currentSourceName = EventLog.LogNameFromSourceName(fixedSource, MachineName);
                if (!currentSourceName.Equals(Log, StringComparison.CurrentCultureIgnoreCase))
                    CreateEventSourceIfNeeded(fixedSource, false);
            }
        }

        /// <summary>
        /// Writes the specified logging event to the event log.
        /// </summary>
        /// <param name="logEvent">The logging event.</param>
        protected override void Write(LogEventInfo logEvent)
        {
            var message = RenderLogEvent(Layout, logEvent);

            var entryType = GetEntryType(logEvent);

            var eventId = EventId.RenderInt(logEvent, 0, "EventLogTarget.EventId");

            var category = Category.RenderShort(logEvent, 0, "EventLogTarget.Category");

            // limitation of EventLog API
            if (message.Length > MaxMessageLength)
            {
                if (OnOverflow == EventLogTargetOverflowAction.Truncate)
                {
                    message = message.Substring(0, MaxMessageLength);
                    WriteEntry(logEvent,
                        message,
                        entryType,
                        eventId,
                        category);
                }
                else if (OnOverflow == EventLogTargetOverflowAction.Split)
                {
                    for (var offset = 0; offset < message.Length; offset += MaxMessageLength)
                    {
                        var chunk = message.Substring(offset, Math.Min(MaxMessageLength, message.Length - offset));
                        WriteEntry(logEvent,
                            chunk,
                            entryType,
                            eventId,
                            category);
                    }
                }
                else if (OnOverflow == EventLogTargetOverflowAction.Discard)
                {
                    //message will not be written
                }
            }
            else
            {
                WriteEntry(logEvent,
                    message,
                    entryType,
                    eventId,
                    category);
            }
        }

        internal virtual void WriteEntry(LogEventInfo logEventInfo,
            string message,
            EventLogEntryType entryType,
            int eventId,
            short category)
        {
            var eventLog = GetEventLog(logEventInfo);
            eventLog.WriteEntry(message, entryType, eventId, category);
        }

        /// <summary>
        /// Get the entry type for logging the message.
        /// </summary>
        /// <param name="logEvent">The logging event - for rendering the <see cref="EntryType" /></param>
        /// <returns></returns>
        EventLogEntryType GetEntryType(LogEventInfo logEvent)
        {
            if (EntryType != null)
            {
                //try parse, if fail,  determine auto

                var value = RenderLogEvent(EntryType, logEvent);

                if (Enum.TryParse(value, true, out EventLogEntryType eventLogEntryType))
                    return eventLogEntryType;
            }

            // determine auto
            if (logEvent.Level >= LogLevel.Error)
                return EventLogEntryType.Error;

            if (logEvent.Level >= LogLevel.Warn)
                return EventLogEntryType.Warning;

            return EventLogEntryType.Information;
        }

        /// <summary>
        /// Get the source, if and only if the source is fixed.
        /// </summary>
        /// <returns><c>null</c> when not <see cref="SimpleLayout.IsFixedText" /></returns>
        /// <remarks>Internal for unit tests</remarks>
        string GetFixedSource()
        {
            if (Source == null)
                return null;

            var simpleLayout = Source as SimpleLayout;
            if (simpleLayout != null && simpleLayout.IsFixedText)
                return simpleLayout.FixedText;

            return null;
        }

        /// <summary>
        /// Get the eventlog to write to.
        /// </summary>
        /// <param name="logEvent">Event if the source needs to be rendered.</param>
        /// <returns></returns>
        EventLog GetEventLog(LogEventInfo logEvent)
        {
            var renderedSource = RenderSource(logEvent);
            var isCacheUpToDate = eventLogInstance != null && renderedSource == eventLogInstance.Source &&
                eventLogInstance.Log == Log && eventLogInstance.MachineName == MachineName;

            if (!isCacheUpToDate)
                eventLogInstance = new EventLog(Log, MachineName, renderedSource);

            if (MaxKilobytes.HasValue)
                //you need more permissions to set, so don't set by default
                eventLogInstance.MaximumKilobytes = MaxKilobytes.Value;

            return eventLogInstance;
        }

        internal string RenderSource(LogEventInfo logEvent)
            => Source != null ? RenderLogEvent(Source, logEvent) : null;

        /// <summary>
        /// (re-)create a event source, if it isn't there. Works only with fixed sourcenames.
        /// </summary>
        /// <param name="fixedSource">sourcenaam. If source is not fixed (see <see cref="SimpleLayout.IsFixedText" />, then pass <c>null</c> or emptystring.</param>
        /// <param name="alwaysThrowError">always throw an Exception when there is an error</param>
        void CreateEventSourceIfNeeded(string fixedSource, bool alwaysThrowError)
        {
            if (string.IsNullOrEmpty(fixedSource))
            {
                InternalLogger.Debug("Skipping creation of event source because it contains layout renderers");
                //we can only create event sources if the source is fixed (no layout)
                return;
            }

            // if we throw anywhere, we remain non-operational
            try
            {
                if (EventLog.SourceExists(fixedSource, MachineName))
                {
                    var currentLogName = EventLog.LogNameFromSourceName(fixedSource, MachineName);
                    if (!currentLogName.Equals(Log, StringComparison.CurrentCultureIgnoreCase))
                    {
                        // re-create the association between Log and Source
                        EventLog.DeleteEventSource(fixedSource, MachineName);
                        var eventSourceCreationData = new EventSourceCreationData(fixedSource, Log)
                        {
                            MachineName = MachineName
                        };

                        EventLog.CreateEventSource(eventSourceCreationData);
                    }
                }
                else
                {
                    var eventSourceCreationData = new EventSourceCreationData(fixedSource, Log)
                    {
                        MachineName = MachineName
                    };

                    EventLog.CreateEventSource(eventSourceCreationData);
                }
            }
            catch (Exception exception)
            {
                InternalLogger.Error(exception, "Error when connecting to EventLog.");
                if (alwaysThrowError)
                    throw;
            }
        }
    }

    static class LayoutHelpers
    {
        /// <summary>
        /// Render the event info as parse as <c>short</c>
        /// </summary>
        /// <param name="layout">current layout</param>
        /// <param name="logEvent"></param>
        /// <param name="defaultValue">default value when the render </param>
        /// <param name="layoutName">layout name for log message to internal log when logging fails</param>
        /// <returns></returns>
        public static short RenderShort(this Layout layout, LogEventInfo logEvent, short defaultValue, string layoutName)
        {
            if (layout == null)
            {
                InternalLogger.Debug(layoutName + " is null so default value of " + defaultValue);
                return defaultValue;
            }

            if (logEvent == null)
            {
                InternalLogger.Debug(layoutName + ": logEvent is null so default value of " + defaultValue);
                return defaultValue;
            }

            var rendered = layout.Render(logEvent);
            short result;

            // NumberStyles.Integer is default of Convert.ToInt16
            // CultureInfo.InvariantCulture is backwardscomp.
            if (!short.TryParse(rendered, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                InternalLogger.Warn(layoutName + ": parse of value '" + rendered + "' failed, return " + defaultValue);
                return defaultValue;
            }

            return result;
        }

        /// <summary>
        /// Render the event info as parse as <c>int</c>
        /// </summary>
        /// <param name="layout">current layout</param>
        /// <param name="logEvent"></param>
        /// <param name="defaultValue">default value when the render </param>
        /// <param name="layoutName">layout name for log message to internal log when logging fails</param>
        /// <returns></returns>
        public static int RenderInt(this Layout layout, LogEventInfo logEvent, int defaultValue, string layoutName)
        {
            if (layout == null)
            {
                InternalLogger.Debug(layoutName + " is null so default value of " + defaultValue);
                return defaultValue;
            }

            if (logEvent == null)
            {
                InternalLogger.Debug(layoutName + ": logEvent is null so default value of " + defaultValue);
                return defaultValue;
            }

            var rendered = layout.Render(logEvent);
            int result;

            // NumberStyles.Integer is default of Convert.ToInt16
            // CultureInfo.InvariantCulture is backwardscomp.
            if (!int.TryParse(rendered, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                InternalLogger.Warn(layoutName + ": parse of value '" + rendered + "' failed, return " + defaultValue);
                return defaultValue;
            }

            return result;
        }
    }
}
#endif