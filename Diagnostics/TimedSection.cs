using System;
using System.Diagnostics;
using Octopus.Server.Extensibility.HostServices.Diagnostics;

namespace Octopus.Shared.Diagnostics
{
    public class TimedSection : IDisposable
    {
        private readonly Stopwatch stopwatch;
        private readonly long infoThreashold;
        private readonly long warningThreashold;
        private readonly Func<long,string> formatMessage;

        public TimedSection(Func<long, string> formatMessage, long infoThreashold,  long warningThreashold = long.MaxValue)
        {
            this.infoThreashold = infoThreashold;
            this.formatMessage = formatMessage;
            this.warningThreashold = warningThreashold;
            stopwatch = Stopwatch.StartNew();
        }

        public long ElapsedMilliseconds => stopwatch.ElapsedMilliseconds;

        public void Dispose()
        {
            stopwatch.Stop();
            var ms = stopwatch.ElapsedMilliseconds;
            var level = ms >= warningThreashold
                ? LogCategory.Warning
                : ms >= infoThreashold
                    ? LogCategory.Info
                    : LogCategory.Verbose;

            Log.System().Write(level, formatMessage(ms));
        }
    }
}