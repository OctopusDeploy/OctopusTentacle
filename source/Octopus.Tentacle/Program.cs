using System;
using System.Net;
using Autofac;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Properties;
using Octopus.Tentacle.Services;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Time;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Versioning;

namespace Octopus.Tentacle
{
    public class Program : OctopusProgram
    {
        public Program(string[] commandLineArguments) : base("Octopus Deploy: Tentacle",
            OctopusTentacle.Version.ToString(),
            OctopusTentacle.InformationalVersion,
            OctopusTentacle.EnvironmentInformation,
            commandLineArguments)
        {
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;
        }

        protected override ApplicationName ApplicationName => ApplicationName.Tentacle;

        static int Main(string[] args)
        {
            return new Program(args).Run();
        }
    }
}
