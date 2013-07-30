using System;
using System.Threading.Tasks;
using Octopus.Shared.Diagnostics;
using Pipefish;
using Pipefish.Standard;

namespace Octopus.Shared.Communications.Logging
{
    [WellKnownActor("Logger")]
    public class ActivityLoggerActor : Actor, IReceive<LogMessage>
    {
        private readonly IActivityLogStorage logStorage;
        readonly ILog log;

        public ActivityLoggerActor(IActivityLogStorage logStorage, ILog log)
        {
            this.logStorage = logStorage;
            this.log = log;
        }

        public Task ReceiveAsync(LogMessage message)
        {
            switch (message.Category)
            {
                case ActivityLogCategory.Verbose:
                    log.Debug(message.MessageText);
                    break;
                case ActivityLogCategory.Info:
                    log.Info(message.MessageText);
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case ActivityLogCategory.Warning:
                    log.Warn(message.MessageText);
                    break;
                case ActivityLogCategory.Error:
                    log.Error(message.MessageText);
                    break;
            }

            return logStorage.AppendAsync(message);
        }
    }
}