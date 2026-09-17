// ReSharper disable RedundantUsingDirective
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Serilog;

partial class Build
{
    [PublicAPI]
    Target TestWindows => _ => _
        .Executes(() => RunTests(TestFramework, TestRuntime));

    [PublicAPI]
    Target TestLinux => _ => _
        .Executes(() => RunTests(TestFramework, TestRuntime));

    [PublicAPI]
    Target TestOsx => _ => _
        .Executes(() => RunTests(TestFramework, TestRuntime));

    [PublicAPI]
    Target TestIntegration => _ => _
        .Executes(() => RunIntegrationTests(TestFramework, TestRuntime, TestFilter));

    [PublicAPI]
    Target TestLinuxPackages => _ => _
        .Description("Tests installing the .deb and .rpm packages onto all of the Linux target distributions.")
        .Executes(() =>
        {
            void RunLinuxPackageTestsFor(TestConfigurationOnLinuxDistribution testConfiguration)
            {
                Logging.InTest($"{testConfiguration.Framework}/{testConfiguration.RuntimeId}/{testConfiguration.DockerImage}/{testConfiguration.PackageType}", () =>
                {
                    string? archSuffix = null;
                    if (testConfiguration.PackageType == "deb" && testConfiguration.RuntimeId == "linux-x64")
                    {
                        archSuffix = "_amd64";
                    }
                    else if (testConfiguration.PackageType == "rpm" && testConfiguration.RuntimeId == "linux-x64")
                    {
                        archSuffix = ".x86_64";
                    }
                    if (string.IsNullOrEmpty(archSuffix)) throw new NotSupportedException();

                    var searchForTestFileDirectory = ArtifactsDirectory / testConfiguration.PackageType;
                    Log.Information("Searching for files in {SearchForTestFileDirectory}", searchForTestFileDirectory);
                    var packageTypeFilePath = searchForTestFileDirectory.GlobFiles($"*{archSuffix}.{testConfiguration.PackageType}")
                        .Single();
                    var packageFile = Path.GetFileName(packageTypeFilePath);
                    Log.Information("Testing Linux package file {PackageFile}", packageFile);

                    var testScriptsBindMountPoint = RootDirectory / "linux-packages" / "test-scripts";

                    var dockerImage = $"docker.packages.octopushq.com/{testConfiguration.DockerImage}";
                    
                    DockerTasks.DockerPull(settings => settings.SetName(dockerImage));
                    DockerTasks.DockerRun(settings => settings
                        .EnableRm()
                        .EnableTty()
                        .SetImage(dockerImage)
                        .SetEnv(
                            $"VERSION={FullSemVer}",
                            "INPUT_PATH=/input",
                            "OUTPUT_PATH=/output",
                            $"BUILD_NUMBER={FullSemVer}")
                        .SetVolume(
                            $"{testScriptsBindMountPoint}:/test-scripts:ro",
                            $"{ArtifactsDirectory}:/artifacts:ro")
                        .SetArgs("bash", "/test-scripts/test-linux-package.sh", $"/artifacts/{testConfiguration.PackageType}/{packageFile}"));
                });
            }
            
            // Test .deb and .rpm installation on ALL Tentacle Linux targets showing >=0.1% of fleet (i.e. be very thourough in our installer testing).
            // This is based on the spreadsheet that was supplied by (IIRC) Rob Erez and Jarrad Raddon in June 2026.
            // We should update this as we get new data (i.e. once per year) of Tentacle usage on which Linux targets.
            // Yes, there will be double-ups (e.g. debian:latest and debian:13 are the same thing), but the container downloads will be cached.
            // Every distribution below is grouped the same way: its rolling tags first, then the pinned versions split by support status.
            // Rolling tags move with the distribution, so they deliberately carry no support status of their own - only the pinned rows do.
            List<TestConfigurationOnLinuxDistribution> testOnLinuxDistributions =
            [
                // Rolling Amazon Linux tags.
                new (NetCore, "linux-x64", "amazonlinux:latest", "rpm"), // Whichever release Amazon currently "recommends".
                // Supported Amazon Linux versions.
                new (NetCore, "linux-x64", "amazonlinux:2023", "rpm"), // Amazon Linux 2023: EOL June 30, 2029.
                // Out-of-support Amazon Linux versions.
                new (NetCore, "linux-x64", "amazonlinux:2", "rpm"), // Amazon Linux 2: support ENDED June 30, 2026.
                
                // Rolling Debian tags.
                new (NetCore, "linux-x64", "debian:latest", "deb"), // Catches each new Debian release as it officially ships.
                new (NetCore, "linux-x64", "debian:stable", "deb"), // Whichever release is currently "stable".
                new (NetCore, "linux-x64", "debian:oldstable", "deb"), // The release before that.
                new (NetCore, "linux-x64", "debian:oldoldstable", "deb"), // The one before that again, so either on LTS or already EOL, depending on where Debian is in its cycle.
                // Supported Debian versions.
                new (NetCore, "linux-x64", "debian:13", "deb"), // Trixie: LTS support until June 30, 2030.
                new (NetCore, "linux-x64", "debian:12", "deb"), // Bookworm: LTS support until June 30, 2028.
                // Out-of-support Debian versions.
                new (NetCore, "linux-x64", "debian:11", "deb"), // Bullseye: LTS support ENDED August 31, 2026.
                
                // No rolling RedHat tags: there is no redhat/ubi or redhat/ubi:latest tag on Red Hat's Docker Hub profile, so each new version has to be opted into below as it releases.
                // Supported RedHat (aka RHEL, CentOS, Rocky Linux, etc.) versions.
                new (NetCore, "linux-x64", "redhat/ubi10", "rpm"), // aka RHEL 10.x (latest) standard support until May 31, 2030.
                new (NetCore, "linux-x64", "redhat/ubi9", "rpm"), // aka RHEL 9.x (latest) standard support until May 31, 2027.
                // Out-of-support RedHat versions.
                new (NetCore, "linux-x64", "redhat/ubi8", "rpm"), // aka RHEL 8.x (latest) standard support until May 31, 2024.
                
                // Rolling Ubuntu tags.
                new (NetCore, "linux-x64", "ubuntu:rolling", "deb"), // The most recent release of any kind, i.e. also the interim, non-LTS ones.
                new (NetCore, "linux-x64", "ubuntu:latest", "deb"), // Whichever release is the current LTS, so this catches each new LTS as it officially ships.
                // Supported Ubuntu versions.
                new (NetCore, "linux-x64", "ubuntu:26.04", "deb"), // Resolute Raccoon: standard support until May 2031.
                new (NetCore, "linux-x64", "ubuntu:24.04", "deb"), // Noble Numbat: standard support until May 2029.
                new (NetCore, "linux-x64", "ubuntu:22.04", "deb"), // Jammy Jellyfish: standard support until May 2027.
                // Out-of-support Ubuntu versions.
                new (NetCore, "linux-x64", "ubuntu:20.04", "deb"), // Focal Fossa: standard support ENDED May 2025.
                new (NetCore, "linux-x64", "ubuntu:18.04", "deb"), // Bionic Beaver: standard support ENDED May 2023 (also covers "linuxmintd/mint19.3-amd64").
                new (NetCore, "linux-x64", "ubuntu:16.04", "deb"), // Xenial Xerus: standard support ENDED April 2021.
            ];
            
            foreach (var testConfiguration in testOnLinuxDistributions)
            {
                RunLinuxPackageTestsFor(testConfiguration);
            }
        });

    [PublicAPI]
    [SupportedOSPlatform("windows")]
    //todo: move this out of the build script to a proper test project ("smoke tests"?)
    Target TestWindowsInstallers => _ => _
        .Executes(() =>
        {
            Logging.InTest(nameof(TestWindowsInstaller), () =>
            {
                TestDirectory.CreateDirectory();
                TestDirectory.CreateOrCleanDirectory();

                var installers = (ArtifactsDirectory / "msi").GlobFiles("*x64.msi");

                if (!installers.Any())
                {
                    throw new Exception($"Expected to find at least one installer in the directory {ArtifactsDirectory}");
                }
            
                foreach (var installer in installers)
                {
                    TestWindowsInstaller(installer);
                }
            });
        });

    string GetTestName(AbsolutePath installerPath) => Path.GetFileName(installerPath).Replace(".msi", "");

    [SupportedOSPlatform("windows")]
    void TestWindowsInstaller(AbsolutePath installerPath)
    {
        Log.Information($"\n--------------------------------------\nTesting Installer {GetTestName(installerPath)}\n--------------------------------------");        

        var destination = TestDirectory / "install" / GetTestName(installerPath);
        destination.CreateDirectory();

        InstallMsi(installerPath, destination);

        try
        {
            ThenTentacleShouldHaveBeenInstalled(destination);
            ThenTentacleShouldBeRunnable(destination);
            ThenBuiltInUserShouldNotHaveWritePermissions(destination);
        }
        finally
        {
            UninstallMsi(installerPath);
        }
    }

    void ThenTentacleShouldHaveBeenInstalled(string destination)
    {
        var expectedTentacleExe = Path.Combine(destination, "Tentacle.exe");
            
        if (!File.Exists(expectedTentacleExe))
        {
            throw new Exception($"Tentacle was not installed at {expectedTentacleExe}");
        }
        Log.Information($"Tentacle was successfully installed at {expectedTentacleExe}");
    }

    void ThenTentacleShouldBeRunnable(string destination)
    {
        var tentacleExe = Path.Combine(destination, "Tentacle.exe");
        
        Log.Information("Checking if Tentacle can run successfully...");

        using var tentacleProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tentacleExe,
                Arguments = "help",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        tentacleProcess.Start();
        var output = new List<string>();
        while (!tentacleProcess.StandardOutput.EndOfStream)
        {
            output.Add(tentacleProcess.StandardOutput.ReadLine() ?? "");
        }

        if (tentacleProcess.ExitCode != 0)
        {
            throw new Exception($"Tentacle exited with exit code {tentacleProcess.ExitCode}");
        }

        if (!output.Any(o => o.StartsWith("Usage: Tentacle")))
        {
            throw new Exception($"Tentacle output does not look like '{tentacleProcess.StartInfo.Arguments}' argument was provided");
        }

        Log.Information($"Tentacle successfully ran using '{tentacleProcess.StartInfo.Arguments}' argument");
    }

    [SupportedOSPlatform("windows")]
    void ThenBuiltInUserShouldNotHaveWritePermissions(string destination)
    {
        var builtInUsersHaveWriteAccess = DoesSidHaveRightsToDirectory(destination, WellKnownSidType.BuiltinUsersSid, FileSystemRights.AppendData, FileSystemRights.CreateFiles);
        if (builtInUsersHaveWriteAccess)
        {
            throw new Exception($"The installation destination {destination} has write permissions for the user BUILTIN\\Users. Expected write permissions to be removed by the installer.");
        }
        Log.Information($"BUILTIN\\Users do not have write access to {destination}. Hooray!");
    }

    void InstallMsi(AbsolutePath installerPath, AbsolutePath destination)
    {
        var installLogName = TestDirectory / $"{GetTestName(installerPath)}.install.log";

        Log.Information("Installing {InstallerPath} to {Destination}", installerPath, destination);

        var arguments = $"/i {installerPath} /QN INSTALLLOCATION={destination} /L*V {installLogName}";
        Log.Information("Running msiexec {Arguments}", arguments);
        var installationProcess = ProcessTasks.StartProcess("msiexec", arguments);
        installationProcess.WaitForExit();

        installLogName.CopyToDirectory(ArtifactsDirectory, ExistsPolicy.FileOverwrite);
        if (installationProcess.ExitCode != 0) {
            throw new Exception($"The installation process exited with a non-zero exit code ({installationProcess.ExitCode}). Check the log {installLogName} for details.");
        }
    }
    
    void UninstallMsi(AbsolutePath installerPath)
    {
        Log.Information("Uninstalling {InstallerPath}", installerPath);
        var uninstallLogName = TestDirectory / $"{GetTestName(installerPath)}.uninstall.log";

        var arguments = $"/x {installerPath} /QN /L*V {uninstallLogName}";
        Log.Information("Running msiexec {Arguments}", arguments);
        var uninstallProcess = ProcessTasks.StartProcess("msiexec", arguments);
        uninstallProcess.WaitForExit();
        (uninstallLogName).CopyToDirectory(ArtifactsDirectory, ExistsPolicy.FileOverwrite);
    }

    [SupportedOSPlatform("windows")]
    bool DoesSidHaveRightsToDirectory(string directory, WellKnownSidType sid, params FileSystemRights[] rights)
    {
        var destinationInfo = new DirectoryInfo(directory);
        var acl = destinationInfo.GetAccessControl();
        var identifier = new SecurityIdentifier(sid, null);
        return acl
            .GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(r => r.IdentityReference.Value == identifier.Value)
            .Where(r => r.AccessControlType == AccessControlType.Allow)
            .Any(r => rights.Any(right => r.FileSystemRights.HasFlag(right)));
    }
    
    void RunTests(string testFramework, string testRuntime)
    {
        Log.Information("Running test for Framework: {TestFramework} and Runtime: {TestRuntime}", testFramework, testRuntime);

        (ArtifactsDirectory / "teamcity").CreateDirectory();
            
        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var octopusTentacleTestsDirectory = BuildDirectory / "Octopus.Tentacle.Tests" / testFramework / testRuntime;
        var octopusTentacleClientTestsDirectory = BuildDirectory / "Octopus.Tentacle.Client.Tests" / testFramework / testRuntime;
        var octopusTentacleManagerTestsDirectory = BuildDirectory / "Octopus.Manager.Tentacle.Tests" / testFramework / testRuntime;
        var testAssembliesPath = octopusTentacleTestsDirectory.GlobFiles("*.Tests.dll")
            .Union(octopusTentacleClientTestsDirectory.GlobFiles("*.Tests.dll"))
            .Union(octopusTentacleManagerTestsDirectory.GlobFiles("*.Tests.dll"));
        
        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            testAssembliesPath.ForEach(projectPath =>
                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile(projectPath)
                    .SetFramework(testFramework)
                    .SetLoggers("console;verbosity=normal", "teamcity"))
            );
        }
        catch (Exception e)
        {
            Log.Warning("{Message}: {Exception}", e.Message, e.ToString());
        }
    }

    void RunIntegrationTests(string testFramework, string testRuntime, string filter)
    {
        Log.Information("Running test for Framework: {TestFramework} and Runtime: {TestRuntime}", testFramework, testRuntime);

        (ArtifactsDirectory / "teamcity").CreateDirectory();

        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var octopusTentacleTestsDirectory = BuildDirectory / "Octopus.Tentacle.Tests.Integration" / testFramework / testRuntime;
        var testAssembliesPath = octopusTentacleTestsDirectory.GlobFiles("*.Tests.Integration.dll");

        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            testAssembliesPath.ForEach(projectPath =>
                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile(projectPath)
                    .SetFramework(testFramework)
                    .SetFilter(filter)
                    .SetLoggers("console;verbosity=normal", "teamcity"))
            );
        }
        catch (Exception e)
        {
            Log.Warning("{Message}: {Exception}", e.Message, e.ToString());
        }
    }
}
