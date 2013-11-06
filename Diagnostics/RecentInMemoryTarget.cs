using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NLog;
using NLog.Targets;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Util;
using Pipefish.Core;

namespace Octopus.Shared.Diagnostics
{
    [Target("RecentInMemory")]
    public class RecentInMemoryTarget : Target
    {
        public static ConcurrentQueue<ActivityLogEntry> RecentEntries { get; private set; }
        static readonly object RecentEntryCleanupLock = new object();

        static readonly Dictionary<LogLevel, ActivityLogEntryCategory> LevelToCategory = new Dictionary<LogLevel, ActivityLogEntryCategory>
        {
            { LogLevel.Trace,
                ActivityLogEntryCategory.Trace },
            { LogLevel.Debug,
                ActivityLogEntryCategory.Verbose },
            { LogLevel.Info,
                ActivityLogEntryCategory.Info },
            { LogLevel.Warn,
                ActivityLogEntryCategory.Warning },
            { LogLevel.Error,
                ActivityLogEntryCategory.Error },
            { LogLevel.Fatal,
                ActivityLogEntryCategory.Fatal }
        };

        static RecentInMemoryTarget()
        {
            RecentEntries = new ConcurrentQueue<ActivityLogEntry>();
        }

        public int BufferSize { get; set; }

        public RecentInMemoryTarget()
        {
            BufferSize = 100;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            var entry = new ActivityLogEntry(
                new DateTimeOffset(logEvent.TimeStamp),
                GetCategory(logEvent.Level),
                ActorId.Empty,
                logEvent.FormattedMessage,
                logEvent.Exception == null ? null : logEvent.Exception.UnpackFromContainers().ToString());

            RecentEntries.Enqueue(entry);

            lock (RecentEntryCleanupLock)
            {
                if (RecentEntries.Count > BufferSize)
                {
                    ActivityLogEntry old;
                    RecentEntries.TryDequeue(out old);
                }
            }
        }

        ActivityLogEntryCategory GetCategory(LogLevel level)
        {
            ActivityLogEntryCategory category;
            if (LevelToCategory.TryGetValue(level, out category))
                return category;

            return ActivityLogEntryCategory.Info;
        }
    }
}
