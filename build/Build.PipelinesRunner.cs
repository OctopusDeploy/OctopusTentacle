using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    const string PipelinesRunnerCliSample = "Octopus.Pipelines.Runner.CliSample";

    [PublicAPI]
    Target TestPipelinesRunner => _ => _
        .Description("Runs the tests for the pipelines runner.")
        .Executes(() =>
        {
            // The teamcity logger's assemblies are only copied to the test output when the build runs under TeamCity.
            var loggers = TeamCity.Instance != null
                ? new[] { "console;verbosity=normal", "teamcity" }
                : new[] { "console;verbosity=normal" };

            DotNetTest(p => p
                .SetProjectFile(SourceDirectory / "Octopus.Pipelines.Runner.Tests")
                .SetLoggers(loggers));
        });

    [PublicAPI]
    Target CheckPipelinesRunnerAot => _ => _
        .Description("Publishes the pipelines runner CLI sample with Native AOT, fails on any trim/AOT warning, and checks the binary runs a job.")
        .Executes(() =>
        {
            var rid = PipelinesRunnerAotRuntimeIdentifier();
            var project = SourceDirectory / PipelinesRunnerCliSample;
            var tempDirectory = (AbsolutePath)Path.Combine(Path.GetTempPath(), $"pipelines-runner-aot-{Guid.NewGuid():N}");
            tempDirectory.CreateDirectory();

            try
            {
                Log.Information("Publishing {Project} ({Rid}, Native AOT)...", PipelinesRunnerCliSample, rid);

                // Clean first (it covers the Core and Docker references too) so the compile and ILC steps always run;
                // an up-to-date skip could otherwise hide warnings a previous run reported.
                DotNetClean(p => p
                    .SetProject(project)
                    .SetConfiguration("Release")
                    .SetRuntime(rid));

                var publishOutput = DotNetPublish(p => p
                    .SetProject(project)
                    .SetConfiguration("Release")
                    .SetRuntime(rid)
                    .SetProperty("PublishAot", "true")
                    .SetOutput(tempDirectory / "bin"));

                var warnings = publishOutput
                    .Select(o => o.Text)
                    .Where(t => Regex.IsMatch(t, "warning (IL|AOT)"))
                    .Distinct()
                    .OrderBy(t => t, StringComparer.Ordinal)
                    .ToList();
                if (warnings.Count > 0)
                    throw new Exception("Publish produced trim/AOT warnings:" + Environment.NewLine + string.Join(Environment.NewLine, warnings));

                Log.Information("OK: publish produced zero IL/AOT warnings");

                var binary = tempDirectory / "bin" / PipelinesRunnerCliSample;
                Log.Information("Binary size: {Size} bytes", new FileInfo(binary).Length);

                var logRoot = tempDirectory / "logs";
                var (_, hi) = RunPipelinesRunnerBinary(binary, logRoot, "echo", "hi");
                if (hi != "hi")
                    throw new Exception($"'-- echo hi' printed '{hi}', expected 'hi'");
                Log.Information("OK: '-- echo hi' printed hi");

                var times = new List<long>();
                for (var i = 0; i < 5; i++)
                {
                    var (elapsed, _) = RunPipelinesRunnerBinary(binary, logRoot, "true");
                    times.Add(elapsed);
                }

                Log.Information("Run times (ms): {Times}", string.Join(" ", times));
                Log.Information("Median start-to-exit time: {Median} ms", times.OrderBy(t => t).ElementAt(2));
            }
            finally
            {
                tempDirectory.DeleteDirectory();
            }
        });

    [PublicAPI]
    Target PublishPipelinesStepRunner => _ => _
        .Description("Publishes the statically linked step runner for the host's architecture, fails on any trim/AOT warning, and checks it starts in musl and glibc images.")
        .Executes(() =>
        {
            const string project = "Octopus.Pipelines.Runner.StepRunner";
            var rid = PipelinesRunnerAotRuntimeIdentifier();
            var output = ArtifactsDirectory / "pipelines-step-runner" / rid;

            DotNetClean(p => p
                .SetProject(SourceDirectory / project)
                .SetConfiguration("Release")
                .SetRuntime(rid));

            var publishOutput = DotNetPublish(p => p
                .SetProject(SourceDirectory / project)
                .SetConfiguration("Release")
                .SetRuntime(rid)
                .SetOutput(output));

            var warnings = publishOutput
                .Select(o => o.Text)
                .Where(t => Regex.IsMatch(t, "warning (IL|AOT)"))
                .Distinct()
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();
            if (warnings.Count > 0)
                throw new Exception("Publish produced trim/AOT warnings:" + Environment.NewLine + string.Join(Environment.NewLine, warnings));

            var binary = output / "octopus-step-runner";
            Log.Information("Step runner: {Binary} ({Size} bytes)", binary, new FileInfo(binary).Length);

            // Statically linked, so the same file runs whatever the image's C library. Without OCTOPUS_RESULT it
            // exits 3 with a message, which is enough to show it started.
            foreach (var image in new[] { "alpine:3.20", "debian:bookworm-slim" })
            {
                var startInfo = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                foreach (var arg in new[] { "run", "--rm", "--network", "none", "--volume", $"{binary}:/octopus-step-runner:ro", "--entrypoint", "/octopus-step-runner", image })
                    startInfo.ArgumentList.Add(arg);

                using var process = Process.Start(startInfo) ?? throw new Exception("Could not start docker");
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 3 || !stderr.Contains("OCTOPUS_RESULT isn't set"))
                    throw new Exception($"The step runner didn't start in {image}: exit {process.ExitCode}: {stderr}");
                Log.Information("OK: the step runner starts in {Image}", image);
            }
        });

    [PublicAPI]
    Target PackPipelinesRunner => _ => _
        .Description("Packs the NuGet packages the CLI consumes: the pipelines runner Core, Docker, Contracts and Gateway libraries.")
        .Executes(() =>
        {
            (ArtifactsDirectory / "nuget").CreateDirectory();

            foreach (var project in new[] { "Octopus.Pipelines.Runner.Core", "Octopus.Pipelines.Runner.Docker", "Octopus.Pipelines.Runner.Contracts", "Octopus.Pipelines.Runner.Gateway" })
            {
                DotNetPack(p => p
                    .SetProject(SourceDirectory / project / $"{project}.csproj")
                    .SetVersion(FullSemVer)
                    .SetOutputDirectory(ArtifactsDirectory / "nuget")
                    .DisableIncludeSymbols()
                    .SetVerbosity(DotNetVerbosity.normal));
            }
        });

    // Native AOT can't link for a different architecture without a cross toolchain, so use the host's.
    static string PipelinesRunnerAotRuntimeIdentifier()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            throw new Exception("CheckPipelinesRunnerAot only runs on Linux (it needs clang and the host's Native AOT toolchain).");

        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "linux-x64",
            Architecture.Arm64 => "linux-arm64",
            var other => throw new Exception($"CheckPipelinesRunnerAot supports linux-x64 and linux-arm64 only, not {other}.")
        };
    }

    static (long ElapsedMilliseconds, string Stdout) RunPipelinesRunnerBinary(AbsolutePath binary, AbsolutePath logRoot, params string[] command)
    {
        var startInfo = new ProcessStartInfo(binary)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--log-root");
        startInfo.ArgumentList.Add(logRoot);
        startInfo.ArgumentList.Add("--");
        foreach (var part in command)
            startInfo.ArgumentList.Add(part);

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo) ?? throw new Exception($"Could not start {binary}");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        stopwatch.Stop();

        if (process.ExitCode != 0)
            throw new Exception($"'{string.Join(' ', command)}' exited with {process.ExitCode}: {stderr.Result}");

        return (stopwatch.ElapsedMilliseconds, stdout.Trim());
    }
}
