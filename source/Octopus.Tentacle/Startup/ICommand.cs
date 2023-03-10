using System;
using System.IO;
using System.Threading;
using Octopus.Tentacle.Internals.Options;

namespace Octopus.Tentacle.Startup
{
    public interface ICommand
    {
        /// <summary>
        /// The sdout from some commands are consumed by scripts, but we can randomly write messages to stdout via the Log.
        /// For example, if a SQL command takes too long, we write an info message to the Log which could mess up how the stdout is interpreted by a script.
        /// Setting this to true will cause the Console to be removed from the Log so only expected content is written to stdout.
        /// Errors will still be written to stderr.
        /// </summary>
        bool SuppressConsoleLogging { get; }

        /// <summary>
        /// Indicates this command can run as a non-interactive service, like a Windows Service or daemon.
        /// Most commands should always run interactively.
        /// </summary>
        bool CanRunAsService { get; }

        OptionSet Options { get; }
        
        CancellationToken CancellationToken { get; }

        void WriteHelp(TextWriter writer);

        // Common options are provided so that the Help command can inspect them
        void Start(string[] commandLineArguments, ICommandRuntime commandRuntime, OptionSet commonOptions);
        void Stop();
    }
}