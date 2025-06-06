﻿using System;
using System.Collections.Generic;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Util
{
    public interface ICommandLineRunner
    {
        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations, ILog log);

        bool Execute(IEnumerable<CommandLineInvocation> commandLineInvocations,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception);

        bool Execute(CommandLineInvocation commandLineInvocation, ILog log);

        bool Execute(CommandLineInvocation invocation,
            Action<string> debug,
            Action<string> info,
            Action<string> error,
            Action<Exception, string> exception);
    }
}