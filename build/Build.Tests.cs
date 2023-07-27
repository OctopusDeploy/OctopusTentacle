// ReSharper disable RedundantUsingDirective
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        .Executes(() => RunIntegrationTests(TestFramework, TestRuntime));

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

                    DockerTasks.DockerPull(settings => settings.SetName(testConfiguration.DockerImage));
                    DockerTasks.DockerRun(settings => settings
                        .EnableRm()
                        .EnableTty()
                        .SetImage(testConfiguration.DockerImage)
                        .SetEnv(
                            $"VERSION={OctoVersionInfo.FullSemVer}",
                            "INPUT_PATH=/input",
                            "OUTPUT_PATH=/output",
                            $"BUILD_NUMBER={OctoVersionInfo.FullSemVer}")
                        .SetVolume(
                            $"{testScriptsBindMountPoint}:/test-scripts:ro",
                            $"{ArtifactsDirectory}:/artifacts:ro")
                        .SetArgs("bash", "/test-scripts/test-linux-package.sh", $"/artifacts/{testConfiguration.PackageType}/{packageFile}"));
                });
            }
            
            List<TestConfigurationOnLinuxDistribution> testOnLinuxDistributions = new()
            {
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "debian:buster", "deb"),

                // Debian dist oldoldstable appears to have been removed on 23/4/2023, maybe temporarily whilst a new stable release is created. 
                // TODO: Revisit this when/if the dist becomes available again and reinstate if possible.
                // new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "debian:oldoldstable-slim", "deb"),

                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "debian:oldstable-slim", "deb"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "debian:stable-slim", "deb"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "linuxmintd/mint19.3-amd64", "deb"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:latest", "deb"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:rolling", "deb"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:jammy", "deb"), // 22.04
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:focal", "deb"), // 20.04
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:bionic", "deb"), // 18.04
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:xenial", "deb"), // 16.04
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "ubuntu:trusty", "deb"), // 14.04
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "centos:7", "rpm"),
                // new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "fedora:latest", "rpm"), // Fedora 36 doesn't support netcore, related https://github.com/dotnet/core/issues/7467 (there is no issue for Fedora 36)
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "fedora:35", "rpm"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "roboxes/rhel7", "rpm"),
                new TestConfigurationOnLinuxDistribution(NetCore, "linux-x64", "roboxes/rhel8", "rpm"),
            };
            
            foreach (var testConfiguration in testOnLinuxDistributions)
            {
                RunLinuxPackageTestsFor(testConfiguration);
            }
        });

    [PublicAPI]
    //todo: move this out of the build script to a proper test project ("smoke tests"?)
    Target TestWindowsInstallerPermissions => _ => _
        .Executes(() =>
        {
            string GetTestName(AbsolutePath installerPath) => Path.GetFileName(installerPath).Replace(".msi", "");
            
            void TestInstallerPermissions(AbsolutePath installerPath)
            {
                var destination = TestDirectory / "install" / GetTestName(installerPath);
                FileSystemTasks.EnsureExistingDirectory(destination);

                InstallMsi(installerPath, destination);

                try
                {
                    var builtInUsersHaveWriteAccess = DoesSidHaveRightsToDirectory(destination, WellKnownSidType.BuiltinUsersSid, FileSystemRights.AppendData, FileSystemRights.CreateFiles);
                    if (builtInUsersHaveWriteAccess)
                    {
                        throw new Exception($"The installation destination {destination} has write permissions for the user BUILTIN\\Users. Expected write permissions to be removed by the installer.");
                    }
                }
                finally
                {
                    UninstallMsi(installerPath);
                }
                
                Log.Information($"BUILTIN\\Users do not have write access to {destination}. Hooray!");
            }

            void InstallMsi(AbsolutePath installerPath, AbsolutePath destination)
            {
                var installLogName = Path.Combine(TestDirectory, $"{GetTestName(installerPath)}.install.log");

                Log.Information($"Installing {installerPath} to {destination}");

                var arguments = $"/i {installerPath} /QN INSTALLLOCATION={destination} /L*V {installLogName}";
                Log.Information($"Running msiexec {arguments}");
                var installationProcess = ProcessTasks.StartProcess("msiexec", arguments);
                installationProcess.WaitForExit();
                FileSystemTasks.CopyFileToDirectory(installLogName, ArtifactsDirectory);
                if (installationProcess.ExitCode != 0) {
                    throw new Exception($"The installation process exited with a non-zero exit code ({installationProcess.ExitCode}). Check the log {installLogName} for details.");
                }
            }
            
            void UninstallMsi(AbsolutePath installerPath)
            {
                Log.Information($"Uninstalling {installerPath}");
                var uninstallLogName = Path.Combine(TestDirectory, $"{GetTestName(installerPath)}.uninstall.log");

                var arguments = $"/x {installerPath} /QN /L*V {uninstallLogName}";
                Log.Information($"Running msiexec {arguments}");
                var uninstallProcess = ProcessTasks.StartProcess("msiexec", arguments);
                uninstallProcess.WaitForExit();
                FileSystemTasks.CopyFileToDirectory(uninstallLogName, ArtifactsDirectory);
            }
            
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

            Logging.InTest(nameof(TestWindowsInstallerPermissions), () =>
            {
                FileSystemTasks.EnsureExistingDirectory(TestDirectory);
                FileSystemTasks.EnsureCleanDirectory(TestDirectory);

                var installers = (ArtifactsDirectory / "msi").GlobFiles("*x64.msi");

                if (!installers.Any())
                {
                    throw new Exception($"Expected to find at least one installer in the directory {ArtifactsDirectory}");
                }
            
                foreach (var installer in installers)
                {
                    TestInstallerPermissions(installer);
                }
            });
        });
    
    void RunTests(string testFramework, string testRuntime)
    {
        Log.Information("Running test for Framework: {TestFramework} and Runtime: {TestRuntime}", testFramework, testRuntime);

        FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "teamcity");
            
        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var octopusTentacleTestsDirectory = BuildDirectory / "Octopus.Tentacle.Tests" / testFramework / testRuntime;
        var testAssembliesPath = octopusTentacleTestsDirectory.GlobFiles("*.Tests.dll");
        var testResultsPath = ArtifactsDirectory / "teamcity" / $"TestResults-Tests-{testFramework}-{testRuntime}.xml";
        
        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            testAssembliesPath.ForEach(projectPath =>
                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile(projectPath)
                    .SetFramework(testFramework)
                    .SetLoggers($"trx;LogFileName={testResultsPath}")
                    .EnableNoBuild())
            );
        }
        catch (Exception e)
        {
            Log.Warning("{Message}: {Exception}", e.Message, e.ToString());
        }
    }

    void RunIntegrationTests(string testFramework, string testRuntime)
    {
        Log.Information("Running test for Framework: {TestFramework} and Runtime: {TestRuntime}", testFramework, testRuntime);

        FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "teamcity");

        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var octopusTentacleTestsDirectory = BuildDirectory / "Octopus.Tentacle.Tests.Integration" / testFramework / testRuntime;
        var testAssembliesPath = octopusTentacleTestsDirectory.GlobFiles("*.Tests*.dll");
        var testResultsPath = ArtifactsDirectory / "teamcity" / $"TestResults-Tests-Integration-{testFramework}-{testRuntime}.xml";

        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            testAssembliesPath.ForEach(projectPath =>
                DotNetTasks.DotNetTest(settings => settings
                    .SetProjectFile(projectPath)
                    .SetFramework(testFramework)
                    .SetLoggers($"trx;LogFileName={testResultsPath}")
                    .EnableNoBuild())
            );
        }
        catch (Exception e)
        {
            Log.Warning("{Message}: {Exception}", e.Message, e.ToString());
        }
    }
}