using System;
using System.Linq;
using NUnit.Framework;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Versioning
{
    [TestFixture]
    public class VersioningFixture
    {
        [Test]
        public void TheTestAssemblyIsVersioned_WithGitVersion()
        {
            if (!TestExecutionContext.IsRunningInTeamCity) Assert.Ignore("We only care about non-0.0.0.0 versions for release builds.");

            Assert.That(GetType().Assembly.GetSemanticVersionInfo().SemanticVersion.Major, Is.GreaterThan(1));
        }

        readonly string[] allowedToBeDifferent = new[]
        {
            "Octopus.Client",
            "Octopus.Configuration",
            "Octopus.Data",
            "Octopus.Diagnostics",
            "Octopus.Server.Extensibility",
            "Octopus.Server.Extensibility.Authentication",
            "Octopus.Time",
        };

        bool IsInAssemblyNamesAllowedToHaveDifferentVersion(string assemblyName)
        {
            return allowedToBeDifferent.Any(assemblyName.StartsWith);
        }

        [Test]
        public void AllOctopusAssemblies_ShouldHaveTheSameInformationalVersion()
        {
            if (!TestExecutionContext.IsRunningInTeamCity) Assert.Ignore("We only care about consistent versions for release builds.");

            var allOctopusAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("Octopus", StringComparison.InvariantCultureIgnoreCase) && !IsInAssemblyNamesAllowedToHaveDifferentVersion(a.GetName().Name))
                .ToArray();

            var fullBuildMetadatas = allOctopusAssemblies.Select(x => new { AssemblyName = x.GetName().Name, Version = x.GetInformationalVersion()}).ToArray();
            Console.WriteLine(string.Join(Environment.NewLine, fullBuildMetadatas.Select(x => $"{x.AssemblyName}: {x.Version}")));
            Assert.That(fullBuildMetadatas.Select(x => x.Version).Distinct().Count(), Is.EqualTo(1), "All of the Octopus Assemblies should have the same version.");
        }
    }
}