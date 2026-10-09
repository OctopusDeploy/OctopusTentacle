// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Chocolatey;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.Octopus;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    const string KubernetesTentacleContainerRuntimeDepsTag = "10.0-trixie-slim";

    //We don't sign linux packages when building locally
    readonly bool SignLinuxPackages = !IsLocalBuild;

    [Parameter("Used to set a custom docker builder when executing DockerBuildxBuild tasks",
        Name = "DockerBuilder")]
    string? DockerBuildxBuilder;

    [Parameter("Specifies the platforms to build the docker images in. Multiple platforms must be comma-separated. Defaults to 'linux/arm64,linux/amd64'.",
        Name = "DockerPlatform")]
    string DockerPlatform = "linux/arm64,linux/amd64";

    [PathVariable] readonly Tool Multipass = null!;

    // Artifact names use "self-contained" in place of the .NET version (e.g. net8.0-windows becomes self-contained-windows),
    // so they don't change with every .NET upgrade. The .NET builds bundle their runtime, so the version isn't a prerequisite.
    static string FrameworkLabel(string framework) => framework switch
    {
        NetFramework => framework,
        NetCoreWindows => "self-contained-windows",
        _ => "self-contained"
    };

    string ArchiveFileName(string framework, string runtimeId, string fileExtension) => $"tentacle-{FullSemVer}-{FrameworkLabel(framework)}-{runtimeId}.{fileExtension}";

    static string BundledArchiveFileName(string framework, string runtimeId, string fileExtension) => $"tentacle-{FrameworkLabel(framework)}-{runtimeId}.{fileExtension}";

    static string MsiFileSuffix(string framework, MSBuildTargetPlatform platform) => framework switch
    {
        NetFramework => platform == MSBuildTargetPlatform.x64 ? "-x64" : "",
        _ => $"-{FrameworkLabel(framework)}-win-{platform}"
    };

    string MsiFileName(string framework, MSBuildTargetPlatform platform) => $"Octopus.Tentacle.{FullSemVer}{MsiFileSuffix(framework, platform)}.msi";

    static string BundledMsiFileName(string framework, MSBuildTargetPlatform platform) => $"Octopus.Tentacle{MsiFileSuffix(framework, platform)}.msi";

    static string BundledUpgraderFileName(string runtimeId) => $"Octopus.Tentacle.Upgrader-{FrameworkLabel(NetCore)}-{runtimeId}.exe";

    [PublicAPI]
    Target PackOsxTarballs => _ => _
        .Description("Packs the OS/X tarballs containing the published binaries.")
        .DependsOn(BuildOsx)
        .Executes(() =>
        {
            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("osx-")))
            {
                PackTarballs(NetCore, runtimeId);
            }
        });

    [PublicAPI]
    Target PackOsx => _ => _
        .Description("Packs all the OSX targets.")
        .DependsOn(PackOsxTarballs);

    [PublicAPI]
    Target PackLinuxTarballs => _ => _
        .Description("Packs the Linux tarballs containing the published binaries.")
        .DependsOn(BuildLinux)
        .Executes(() =>
        {
            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("linux-")))
            {
                PackTarballs(NetCore, runtimeId);
            }
        });

    [PublicAPI]
    Target PackLinuxPackagesLegacy => _ => _
        .Description("Legacy task until we can split creation of .rpm and .deb packages into their own tasks")
        .DependsOn(PackLinuxTarballs)
        .Requires(
            () => !SignLinuxPackages || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PRIVATE_KEY")),
            () => !SignLinuxPackages || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PASSPHRASE")))
        .Executes(() =>
        {
            const string dockerToolsContainerImage = "docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest";

            //this is just to stop messages such as scout vulnerability hints which are reported as errors (but don't actually fail anything)
            Environment.SetEnvironmentVariable("DOCKER_CLI_HINTS", "false");

            void CreateLinuxPackages(string runtimeId)
            {
                //TODO It's probable that the .deb and .rpm package layouts will be different - and potentially _should already_ be different.
                // We're approaching this with the assumption that we'll split .deb and .rpm creation soon, which means that we'll create a separate
                // filesystem layout for each of them. Using .deb for now; expecting to replicate that soon for .rpm.
                var debBuildDir = BuildDirectory / "deb" / runtimeId;
                (debBuildDir / "scripts").CreateDirectory();
                (debBuildDir / "output").CreateDirectory();

                var packagingScriptsDirectory = RootDirectory / "linux-packages" / "packaging-scripts";

                // if we aren't signing, use the unsigned scripts
                if (!SignLinuxPackages)
                    packagingScriptsDirectory /= "unsigned";

                packagingScriptsDirectory.GlobFiles("*")
                    .ForEach(x => x.CopyToDirectory(debBuildDir / "scripts"));

                DockerTasks.DockerPull(settings => settings
                    .When(_ => RuntimeInformation.OSArchitecture == Architecture.Arm64, _ => _.SetPlatform("linux/amd64"))
                    .SetName(dockerToolsContainerImage));

                DockerTasks.DockerRun(settings => settings
                    .When(_ => RuntimeInformation.OSArchitecture == Architecture.Arm64, _ => _.SetPlatform("linux/amd64"))
                    .EnableRm()
                    .EnableTty()
                    .SetEnv(
                        $"VERSION={FullSemVer}",
                        "INPUT_PATH=/input",
                        "OUTPUT_PATH=/output",
                        "SIGN_PRIVATE_KEY",
                        "SIGN_PASSPHRASE")
                    .SetVolume(
                        $"{debBuildDir / "scripts"}:/scripts",
                        $"{BuildDirectory / "zip" / NetCore / runtimeId / "tentacle"}:/input",
                        $"{debBuildDir / "output"}:/output"
                    )
                    .SetImage(dockerToolsContainerImage)
                    .SetCommand("bash")
                    .SetArgs("/scripts/package.sh", runtimeId));
            }

            var targetRuntimeIds = RuntimeIds.Where(x => x.StartsWith("linux-"))
                .Where(x => x != "linux-musl-x64"); // not supported yet. Work in progress.

            foreach (var runtimeId in targetRuntimeIds)
            {
                CreateLinuxPackages(runtimeId);

                var debOutputDirectory = BuildDirectory / "deb" / runtimeId / "output";

                (ArtifactsDirectory / "deb").CreateDirectory();
                debOutputDirectory.GlobFiles("*.deb")
                    .ForEach(x => x.CopyToDirectory(ArtifactsDirectory / "deb"));

                CopyDebianPackageToDockerFolder(runtimeId);

                (ArtifactsDirectory / "rpm").CreateDirectory();
                debOutputDirectory.GlobFiles("*.rpm")
                    .ForEach(x => x.CopyToDirectory(ArtifactsDirectory / "rpm"));
            }
        });

    [PublicAPI]
    Target PackDebianPackage => _ => _
        .Description("TODO: Move .deb creation into this task")
        .DependsOn(PackLinuxPackagesLegacy);

    [PublicAPI]
    Target BuildAndPushKubernetesTentacleContainerImage => _ => _
        .Description("Builds and pushes the kubernetes tentacle multi-arch container image")
        .Executes(() =>
        {
            //Debian 12
            BuildAndPushOrLoadKubernetesTentacleContainerImage(push: true, load: false, KubernetesTentacleContainerRuntimeDepsTag, "docker.packages.octopushq.com");
        });

    [PublicAPI]
    Target BuildAndLoadLocallyKubernetesTentacleContainerImage => _ => _
        .Description("Builds and loads locally the kubernetes tentacle multi-arch container image")
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(PackDebianPackage)
        .Executes(() =>
        {
            BuildAndPushOrLoadKubernetesTentacleContainerImage(push: false, load: true, KubernetesTentacleContainerRuntimeDepsTag);
        });

    [PublicAPI]
    Target BuildAndPushForMicrok8sKubernetesTentacleContainerImage => _ => _
        .Description("Builds and loads into microk8s the kubernetes tentacle multi-arch container image")
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(PackDebianPackage)
        .Executes(() =>
        {
            var host = GetMicrok8sIpAddress();
            const int port = 32000;
            var hostPort = $"{host}:{port}";

            BuildAndPushOrLoadKubernetesTentacleContainerImage(push: true, load: false, KubernetesTentacleContainerRuntimeDepsTag, host: hostPort);
        });

    [PublicAPI]
    Target BuildAndLoadLocalDebugKubernetesTentacleContainerImage => _ => _
        .Description("Builds and loads locally the kubernetes tentacle multi-arch container image")
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(PackDebianPackage)
        .Executes(() =>
        {
            BuildAndPushOrLoadKubernetesTentacleContainerImage(push: false, load: true, KubernetesTentacleContainerRuntimeDepsTag, includeDebugger: true);
        });

    [PublicAPI]
    Target PackRedHatPackage => _ => _
        .Description("TODO: Move .rpm creation into this task")
        .DependsOn(PackLinuxPackagesLegacy);

    [PublicAPI]
    Target PackLinux => _ => _
        .Description("Packs all the Linux targets.")
        .DependsOn(PackDebianPackage)
        .DependsOn(PackRedHatPackage);

    [PublicAPI]
    Target PackLinuxUnsigned => _ => _
        .Description("Packages all the Linux targets without signing the packages.")
        .DependsOn(PackDebianPackage)
        .DependsOn(PackRedHatPackage)
        .OnlyWhenStatic(() => IsLocalBuild)
        .OnlyWhenStatic(() => !SignLinuxPackages);

    [PublicAPI]
    Target PackWindowsZips => _ => _
        .Description("Packs the Windows .zip files containing the published binaries.")
        .DependsOn(BuildWindows)
        .Executes(() =>
        {
            (ArtifactsDirectory / "zip").CreateOrCleanDirectory();

            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("win")))
            {
                switch (runtimeId)
                {
                    case "win":
                        PackWindowsZip(NetFramework, runtimeId);
                        break;
                    case "win-x86":
                    case "win-x64":
                        PackWindowsZip(NetCoreWindows, runtimeId);
                        PackWindowsZip(NetCore, runtimeId);
                        break;
                    default:
                        PackWindowsZip(NetCore, runtimeId);
                        break;
                }
            }
        });

    void PackWindowsZip(string framework, string runtimeId)
    {
        var workingDirectory = BuildDirectory / "zip" / framework / runtimeId;
        var workingTentacleDirectory = workingDirectory / "tentacle";

        workingDirectory.CreateOrCleanDirectory();
        workingTentacleDirectory.CreateOrCleanDirectory();

        (BuildDirectory / "Tentacle" / framework / runtimeId).GlobFiles($"*")
            .ForEach(x => x.CopyToDirectory(workingTentacleDirectory));

        ZipFile.CreateFromDirectory(
            workingDirectory,
            ArtifactsDirectory / "zip" / ArchiveFileName(framework, runtimeId, "zip"));
    }

    [PublicAPI]
    Target PackWindowsInstallers => _ => _
        .Description("Packs the Windows .msi files.")
        .DependsOn(BuildWindows)
        .Executes(() =>
        {
            void PackWindowsInstallers(MSBuildTargetPlatform platform, AbsolutePath wixNugetPackagePath, string framework, string frameworkName)
            {
                var installerDirectory = BuildDirectory / "Installer";
                installerDirectory.CreateDirectory();
                installerDirectory.CreateOrCleanDirectory();

                if (framework == NetFramework)
                {
                    (BuildDirectory / "Tentacle" / framework / "win").GlobFiles("*")
                        .ForEach(x => x.CopyToDirectory(installerDirectory, ExistsPolicy.FileOverwrite));

                    (BuildDirectory / "Octopus.Manager.Tentacle" / framework / "win").GlobFiles("*")
                        .ForEach(x => x.CopyToDirectory(installerDirectory, ExistsPolicy.FileOverwrite));
                }
                else if (framework is NetCoreWindows)
                {
                    (BuildDirectory / "Tentacle" / framework / $"win-{platform}").GlobFiles("*")
                        .ForEach(x => x.CopyToDirectory(installerDirectory, ExistsPolicy.FileOverwrite));

                    (BuildDirectory / "Octopus.Manager.Tentacle" / framework / $"win-{platform}").GlobFiles("*")
                        .ForEach(x => x.CopyToDirectory(installerDirectory, ExistsPolicy.FileOverwrite));
                }
                else
                {
                    (BuildDirectory / "Tentacle" / framework / $"win-{platform}").GlobFiles("*")
                        .ForEach(x => x.CopyToDirectory(installerDirectory, ExistsPolicy.FileOverwrite));
                }

                var harvestFilePath = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Tentacle.Generated.wxs";

                using var harvestFile = new ModifiableFileWithRestoreContentsOnDispose(harvestFilePath);
                GenerateMsiInstallerContents(installerDirectory, harvestFile.FilePath);
                BuildMsiInstallerForPlatform(platform, wixNugetPackagePath, framework, frameworkName);
            }

            void GenerateMsiInstallerContents(AbsolutePath installerDirectory, AbsolutePath harvestFile)
            {
                Logging.InBlock("Running HEAT to generate the installer contents...", () =>
                {
                    var harvestDirectory = installerDirectory;

                    var heatArguments = $"dir {harvestDirectory} " +
                        "-nologo " +
                        "-gg " + //GenerateGuid
                        "-sfrag " + //SuppressFragments
                        "-srd " + //SuppressRootDirectory
                        "-sreg " + //SuppressRegistry
                        "-suid " + //SuppressUniqueIds
                        "-cg TentacleComponents " + //ComponentGroupName
                        "-var var.TentacleSource " + //PreprocessorVariable
                        "-dr INSTALLLOCATION " + //DirectoryReferenceId
                        $"-out {harvestFile}";

                    WiXHeatTool(heatArguments);
                });
            }

            void BuildMsiInstallerForPlatform(MSBuildTargetPlatform platform, AbsolutePath wixNugetPackagePath, string framework, string frameworkName)
            {
                Logging.InBlock($"Building {framework}-{platform} installer", () =>
                {
                    var tentacleInstallerWixProject = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Octopus.Tentacle.Installer.wixproj";
                    using var wixProjectFile = new ModifiableFileWithRestoreContentsOnDispose(tentacleInstallerWixProject);

                    wixProjectFile.ReplaceTextInFile("{WixToolPath}", wixNugetPackagePath / "tools");
                    wixProjectFile.ReplaceTextInFile("{WixTargetsPath}", wixNugetPackagePath / "tools" / "Wix.targets");
                    wixProjectFile.ReplaceTextInFile("{WixTasksPath}", wixNugetPackagePath / "tools" / "wixtasks.dll");

                    var tentacleInstallerWixProduct = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Product.wxs";
                    using var wixProductFile = new ModifiableFileWithRestoreContentsOnDispose(tentacleInstallerWixProduct);
                    wixProductFile.ReplaceTextInFile("{TargetFramework}", frameworkName);

                    MSBuildTasks.MSBuild(settings => settings
                        .SetConfiguration("Release")
                        .SetProperty("AllowUpgrade", "True")
                        .SetVerbosity(MSBuildVerbosity.Normal) //TODO: Set verbosity from command line argument
                        .SetTargets("build")
                        .SetTargetPlatform(platform)
                        .SetTargetPath(RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Octopus.Tentacle.Installer.wixproj"));

                    var builtMsi = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "bin" / platform / "Octopus.Tentacle.msi";
                    Signing.Sign(builtMsi);

                    builtMsi.Move(ArtifactsDirectory / "msi" / MsiFileName(framework, platform));
                });
            }

            // This is a slow operation
            var wixNugetInstalledPackage = NuGetPackageResolver.GetLocalInstalledPackage("wix", NuGetToolPathResolver.NuGetPackagesConfigFile);
            if (wixNugetInstalledPackage == null) throw new Exception("Failed to find wix nuget package path");

            (ArtifactsDirectory / "msi").CreateDirectory();

            PackWindowsInstallers(MSBuildTargetPlatform.x64, wixNugetInstalledPackage.Directory, NetFramework, "NetFramework");
            PackWindowsInstallers(MSBuildTargetPlatform.x86, wixNugetInstalledPackage.Directory, NetFramework, "NetFramework");

            PackWindowsInstallers(MSBuildTargetPlatform.x64, wixNugetInstalledPackage.Directory, NetCore, "NetCore");
            PackWindowsInstallers(MSBuildTargetPlatform.x86, wixNugetInstalledPackage.Directory, NetCore, "NetCore");

            PackWindowsInstallers(MSBuildTargetPlatform.x64, wixNugetInstalledPackage.Directory, NetCoreWindows, "NetCoreWindows");
            PackWindowsInstallers(MSBuildTargetPlatform.x86, wixNugetInstalledPackage.Directory, NetCoreWindows, "NetCoreWindows");
        });

    [PublicAPI]
    Target PackChocolateyPackage => _ => _
        .Description("Packs the Chocolatey installer.")
        .DependsOn(PackWindowsInstallers)
        .Executes(() =>
        {
            (ArtifactsDirectory / "chocolatey").CreateDirectory();

            var chocolateyNetFrameworkSourceDirectory = SourceDirectory / "Chocolatey-Net-Framework";
            const string chocolateyNetFrameworkNuspecFileName = "OctopusDeploy.Tentacle.nuspec";
            PackChocolateyPackageToArtifactsDirectory(NetFramework, chocolateyNetFrameworkSourceDirectory, chocolateyNetFrameworkNuspecFileName);

            var chocolateySelfContainedSourceDirectory = SourceDirectory / "Chocolatey-Self-Contained";
            const string chocolateySelfContainedNuspecFileName = "OctopusDeploy.Tentacle.SelfContained.nuspec";
            PackChocolateyPackageToArtifactsDirectory(NetCoreWindows, chocolateySelfContainedSourceDirectory, chocolateySelfContainedNuspecFileName);
        });

    void PackChocolateyPackageToArtifactsDirectory(string framework, AbsolutePath chocolateySourceDirectory, string pathToChocolateyNuspec)
    {
        var x86MsiFileName = MsiFileName(framework, MSBuildTargetPlatform.x86);
        var md5ChecksumNetFramework = (ArtifactsDirectory / "msi" / x86MsiFileName).GetFileHash();
        Log.Information($"MD5 Checksum: {x86MsiFileName} = {md5ChecksumNetFramework}");

        var x64MsiFileName = MsiFileName(framework, MSBuildTargetPlatform.x64);
        var md5ChecksumX64NetFramework = (ArtifactsDirectory / "msi" / x64MsiFileName).GetFileHash();
        Log.Information($"Checksum: {x64MsiFileName} = {md5ChecksumX64NetFramework}");

        var chocolateyNetFrameworkInstallScriptPath = chocolateySourceDirectory / "chocolateyInstall.ps1";
        using var chocolateyInstallScriptFile = new ModifiableFileWithRestoreContentsOnDispose(chocolateyNetFrameworkInstallScriptPath);
        chocolateyInstallScriptFile.ReplaceRegexInFiles("0.0.0", FullSemVer);
        chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksum>", md5ChecksumNetFramework);
        chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksumtype>", "md5");
        chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksum64>", md5ChecksumX64NetFramework);
        chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksumtype64>", "md5");

        ChocolateyTasks.ChocolateyPack(settings => settings
            .SetPathToNuspec(chocolateySourceDirectory / pathToChocolateyNuspec)
            .SetVersion(NuGetVersion)
            .SetOutputDirectory(ArtifactsDirectory / "chocolatey"));
    }

    [PublicAPI]
    Target PackContracts => _ => _
        .Description("Packs the NuGet package for Tentacle contracts.")
        .Executes(() =>
        {
            (ArtifactsDirectory / "nuget").CreateDirectory();
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();

            DotNetPack(p => p
                .SetProject(RootDirectory / "source" / "Octopus.Tentacle.Contracts" / "Octopus.Tentacle.Contracts.csproj")
                .SetVersion(FullSemVer)
                .SetOutputDirectory(ArtifactsDirectory / "nuget")
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.normal)
                .SetProperty("NuspecProperties", $"Version={FullSemVer}"));
        });

    [PublicAPI]
    Target PackCore => _ => _
        .Description("Packs the NuGet package for Tentacle core.")
        .DependsOn(PackContracts)
        .Executes(() =>
        {
            (ArtifactsDirectory / "nuget").CreateDirectory();
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();

            DotNetPack(p => p
                .SetProject(RootDirectory / "source" / "Octopus.Tentacle.Core" / "Octopus.Tentacle.Core.csproj")
                .SetVersion(FullSemVer)
                .SetOutputDirectory(ArtifactsDirectory / "nuget")
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.normal)
                .SetProperty("NuspecProperties", $"Version={FullSemVer}"));
        });

    [PublicAPI]
    Target PackClient => _ => _
        .Description("Packs the NuGet package for Tentacle Client.")
        .Executes(() =>
        {
            (ArtifactsDirectory / "nuget").CreateDirectory();
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();

            DotNetPack(p => p
                .SetProject(RootDirectory / "source" / "Octopus.Tentacle.Client" / "Octopus.Tentacle.Client.csproj")
                .SetVersion(FullSemVer)
                .SetOutputDirectory(ArtifactsDirectory / "nuget")
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.normal)
                .SetProperty("NuspecProperties", $"Version={FullSemVer}"));
        });

    [PublicAPI]
    Target PackWindows => _ => _
        .Description("Packs all the Windows targets.")
        .DependsOn(PackWindowsZips)
        .DependsOn(PackChocolateyPackage);

    [PublicAPI]
    Target PackCrossPlatformBundle => _ => _
        .Description("Packs the cross-platform Tentacle.nupkg used by Octopus Server to dynamically upgrade Tentacles.")
        .Executes(() =>
        {
            string ConstructDebianPackageFilename(string packageName, string architecture) => $"{packageName}_{FullSemVer}_{architecture}.deb";

            string ConstructRedHatPackageFilename(string packageName, string architecture)
            {
                var transformedVersion = FullSemVer.Replace("-", "_");
                var filename = $"{packageName}-{transformedVersion}-1.{architecture}.rpm";
                return filename;
            }

            (ArtifactsDirectory / "nuget").CreateDirectory();

            var workingDirectory = BuildDirectory / "Octopus.Tentacle.CrossPlatformBundle";
            workingDirectory.CreateDirectory();

            // Get the installers for Tentacle: .NET Framework 4.8, self-contained w/o Tentacle Manager, and self-contained w/ Tentacle Manager
            var msiFiles = CopyMsis(workingDirectory, NetFramework, NetCore, NetCoreWindows);

            // Get the self-contained Tentacle Upgrader
            var upgraderFiles = CopySelfContainedUpgraders(workingDirectory);

            // Get all .NET Framework 4.8 files (installers and archives) for Tentacle Upgrader
            var octopusTentacleUpgraderDirectory = BuildDirectory / "Octopus.Tentacle.Upgrader" / NetFramework / "win";
            octopusTentacleUpgraderDirectory.GlobFiles("*").ForEach(x => x.CopyToDirectory(workingDirectory));

            // Get all DEB packages for Linux
            var debAmd64PackageFilename = ConstructDebianPackageFilename("tentacle", "amd64");
            var debArm64PackageFilename = ConstructDebianPackageFilename("tentacle", "arm64");
            var debArm32PackageFilename = ConstructDebianPackageFilename("tentacle", "armhf");
            (ArtifactsDirectory / "deb" / debAmd64PackageFilename).Copy(workingDirectory / debAmd64PackageFilename);
            (ArtifactsDirectory / "deb" / debArm64PackageFilename).Copy(workingDirectory / debArm64PackageFilename);
            (ArtifactsDirectory / "deb" / debArm32PackageFilename).Copy(workingDirectory / debArm32PackageFilename);

            // Get all RPM packages for Linux
            var rpmArm64PackageFilename = ConstructRedHatPackageFilename("tentacle", "aarch64");
            var rpmArm32PackageFilename = ConstructRedHatPackageFilename("tentacle", "armv7hl");
            var rpmx64PackageFilename = ConstructRedHatPackageFilename("tentacle", "x86_64");
            (ArtifactsDirectory / "rpm" / rpmArm64PackageFilename).Copy(workingDirectory / rpmArm64PackageFilename);
            (ArtifactsDirectory / "rpm" / rpmArm32PackageFilename).Copy(workingDirectory / rpmArm32PackageFilename);
            (ArtifactsDirectory / "rpm" / rpmx64PackageFilename).Copy(workingDirectory / rpmx64PackageFilename);

            // Get the archives for all runtimes
            foreach (var framework in new[] { NetFramework, NetCore })
            {
                foreach (var runtimeId in RuntimeIds)
                {
                    if (runtimeId == "win" && framework != NetFramework
                        || runtimeId != "win" && framework == NetFramework) continue;

                    var fileExtension = runtimeId.StartsWith("win") ? "zip" : "tar.gz";
                    var path = ArtifactsDirectory / "zip" / ArchiveFileName(framework, runtimeId, fileExtension);
                    path.Copy(workingDirectory / BundledArchiveFileName(framework, runtimeId, fileExtension));
                }
            }

            // Get the archives for Tentacle built against the Windows-specific target framework
            (ArtifactsDirectory / "zip" / ArchiveFileName(NetCoreWindows, "win-x86", "zip")).Copy(
                workingDirectory / BundledArchiveFileName(NetCoreWindows, "win-x86", "zip"));
            (ArtifactsDirectory / "zip" / ArchiveFileName(NetCoreWindows, "win-x64", "zip")).Copy(
                workingDirectory / BundledArchiveFileName(NetCoreWindows, "win-x64", "zip"));

            // Assert all the expected files have been successfully copied
            Assert.True((workingDirectory / "Octopus.Tentacle.Upgrader.exe").FileExists(), "Missing Octopus.Tentacle.Upgrader.exe");
            foreach (var expectedFile in msiFiles.Concat(upgraderFiles))
            {
                Assert.True((workingDirectory / expectedFile).FileExists(), $"Missing {expectedFile}");
            }
            Assert.True((workingDirectory / debAmd64PackageFilename).FileExists(), $"Missing {debAmd64PackageFilename}");
            Assert.True((workingDirectory / debArm64PackageFilename).FileExists(), $"Missing {debArm64PackageFilename}");
            Assert.True((workingDirectory / debArm32PackageFilename).FileExists(), $"Missing {debArm32PackageFilename}");
            Assert.True((workingDirectory / rpmArm64PackageFilename).FileExists(), $"Missing {rpmArm64PackageFilename}");
            Assert.True((workingDirectory / rpmArm32PackageFilename).FileExists(), $"Missing {rpmArm32PackageFilename}");
            Assert.True((workingDirectory / rpmx64PackageFilename).FileExists(), $"Missing {rpmx64PackageFilename}");

            const string id = "Octopus.Tentacle.CrossPlatformBundle";

            const string author = "Octopus Deploy";
            const string title = "Octopus Tentacle cross platform bundle";
            const string description = "The deployment agent that is installed on each machine you plan to deploy to using Octopus.";

            CreateNugetPackage(workingDirectory, id, author, title, description);
        });

    [PublicAPI]
    Target PackCrossPlatformBundleForServer => _ => _
        .Description("Packs the cross-platform Tentacle.nupkg used by Octopus Server to dynamically upgrade Tentacles.")
        .Executes(() =>
        {
            (ArtifactsDirectory / "nuget").CreateDirectory();

            var workingDirectory = BuildDirectory / "Octopus.Tentacle.CrossPlatformBundle.Server";
            workingDirectory.CreateDirectory();

            // Get the installers for Tentacle: .NET Framework 4.8, and self-contained w/o Tentacle Manager
            var msiFiles = CopyMsis(workingDirectory, NetFramework, NetCore);

            // Get the self-contained Tentacle Upgrader
            var upgraderFiles = CopySelfContainedUpgraders(workingDirectory);

            // Get the .NET Framework 4.8 installers for the Tentacle Upgrader
            var octopusTentacleUpgraderDirectory = BuildDirectory / "Octopus.Tentacle.Upgrader" / NetFramework / "win";
            octopusTentacleUpgraderDirectory.GlobFiles("*").ForEach(x => x.CopyToDirectory(workingDirectory));

            // Get the archives for all required runtimes
            foreach (var runtimeId in CrossPlatformBundleForServerRequiredRuntimes)
            {
                // We don't need the zip archives for Windows runtimes
                // as we've already manually grabbed the installers
                if (runtimeId.StartsWith("win")) continue;

                var path = ArtifactsDirectory / "zip" / ArchiveFileName(NetCore, runtimeId, "tar.gz");
                path.Copy(workingDirectory / BundledArchiveFileName(NetCore, runtimeId, "tar.gz"));
            }

            // Assert all the expected files have been successfully copied
            Assert.True((workingDirectory / "Octopus.Tentacle.Upgrader.exe").FileExists(), "Missing Octopus.Tentacle.Upgrader.exe");
            foreach (var expectedFile in msiFiles.Concat(upgraderFiles))
            {
                Assert.True((workingDirectory / expectedFile).FileExists(), $"Missing {expectedFile}");
            }

            const string id = "Octopus.Tentacle.CrossPlatformBundle.Server";

            const string author = "Octopus Deploy";
            const string title = "Octopus Tentacle cross platform bundle (for Octopus Server)";
            const string description = "The deployment agent that is installed on each machine you plan to deploy to using Octopus.";

            CreateNugetPackage(workingDirectory, id, author, title, description);
        });

    [PublicAPI]
    Target PackCrossPlatformBundleForDevelopment => _ => _
        .Description("Packs a slim cross-platform Tentacle bundle (Windows x64, macOS ARM64, Linux x64 archives) for local dev environments and integration/E2E test setup.")
        .Executes(() =>
        {
            (ArtifactsDirectory / "nuget").CreateDirectory();

            var workingDirectory = BuildDirectory / "Octopus.Tentacle.CrossPlatformBundle.Development";
            workingDirectory.CreateDirectory();

            // Get the archives (binaries) for all required runtimes
            foreach (var runtimeId in CrossPlatformBundleForDevelopmentRequiredRuntimes)
            {
                var fileExtension = runtimeId.StartsWith("win") ? "zip" : "tar.gz";
                var path = ArtifactsDirectory / "zip" / ArchiveFileName(NetCore, runtimeId, fileExtension);
                path.Copy(workingDirectory / BundledArchiveFileName(NetCore, runtimeId, fileExtension));
            }

            // Assert all the expected files have been successfully copied
            foreach (var expectedFile in new[] { BundledArchiveFileName(NetCore, "win-x64", "zip"), BundledArchiveFileName(NetCore, "osx-arm64", "tar.gz"), BundledArchiveFileName(NetCore, "linux-x64", "tar.gz") })
            {
                Assert.True((workingDirectory / expectedFile).FileExists(), $"Missing {expectedFile}");
            }

            const string id = "Octopus.Tentacle.CrossPlatformBundle.Development";

            const string author = "Octopus Deploy";
            const string title = "Octopus Tentacle cross platform bundle (for local development and testing)";
            const string description = "The deployment agent that is installed on each machine you plan to deploy to using Octopus.";

            CreateNugetPackage(workingDirectory, id, author, title, description);
        });

    string[] CopyMsis(AbsolutePath workingDirectory, params string[] frameworks)
    {
        var platforms = new[] { MSBuildTargetPlatform.x86, MSBuildTargetPlatform.x64 };
        foreach (var framework in frameworks)
        {
            foreach (var platform in platforms)
            {
                (ArtifactsDirectory / "msi" / MsiFileName(framework, platform)).Copy(workingDirectory / BundledMsiFileName(framework, platform));
            }
        }

        return frameworks.SelectMany(framework => platforms.Select(platform => BundledMsiFileName(framework, platform))).ToArray();
    }

    string[] CopySelfContainedUpgraders(AbsolutePath workingDirectory)
    {
        var runtimeIds = new[] { "win-x86", "win-x64" };
        foreach (var runtimeId in runtimeIds)
        {
            (BuildDirectory / "Octopus.Tentacle.Upgrader" / NetCore / runtimeId / "Octopus.Tentacle.Upgrader.exe").Copy(workingDirectory / BundledUpgraderFileName(runtimeId));
        }

        return runtimeIds.Select(BundledUpgraderFileName).ToArray();
    }

    void CreateNugetPackage(
        AbsolutePath workingDirectory,
        string id,
        string author,
        string title,
        string description)
    {
        var outFolder = ArtifactsDirectory / "nuget";
        var octopus = InstallOctopusCli();

        // Note: Nuke automatically escapes this string by using the string interpolation syntax
        ProcessTasks.StartProcess(
            octopus,
            $"package nuget create --id {id} --version {FullSemVer} --base-path {workingDirectory} --out-folder {outFolder} --author {author} --title {title} --description {description} --no-prompt"
        ).WaitForExit();
    }

    [PublicAPI]
    Target Pack => _ => _
        .Description("Pack all the artifacts. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(PackCrossPlatformBundle)
        .DependsOn(PackCrossPlatformBundleForServer)
        .DependsOn(PackCrossPlatformBundleForDevelopment)
        .DependsOn(PackContracts)
        .DependsOn(PackCore)
        .DependsOn(PackClient);

    void PackTarballs(string framework, string runtimeId)
    {
        (ArtifactsDirectory / "zip").CreateDirectory();

        var workingDir = BuildDirectory / "zip" / framework / runtimeId;
        (workingDir / "tentacle").CreateDirectory();

        var linuxPackagesContent = RootDirectory / "linux-packages" / "content";
        var tentacleDirectory = BuildDirectory / "Tentacle" / framework / runtimeId;

        linuxPackagesContent.GlobFiles("*")
            .ForEach(x => x.CopyToDirectory(workingDir / "tentacle"));
        tentacleDirectory.GlobFiles("*")
            .ForEach(x => x.CopyToDirectory(workingDir / "tentacle"));

        TarGZipCompress(
            workingDir,
            "tentacle",
            ArtifactsDirectory / "zip",
            ArchiveFileName(framework, runtimeId, "tar.gz"));
    }

    void BuildAndPushOrLoadKubernetesTentacleContainerImage(bool push, bool load, string runtimeDepsImageTag, string? host = null, bool includeDebugger = false, string? tagSuffix = null)
    {
        var hostPrefix = host is not null ? $"{host}/" : string.Empty;
        DockerTasks.DockerBuildxBuild(settings =>
        {
            if (includeDebugger && DockerPlatform.Contains(','))
                throw new InvalidOperationException("Only a single DockerPlatform can be defined when build the docker image with the debugger");

            if (DockerBuildxBuilder is not null)
                settings = settings.SetBuilder(DockerBuildxBuilder);

            var dockerfile = !includeDebugger
                ? "./docker/kubernetes-agent-tentacle/Dockerfile"
                : "./docker/kubernetes-agent-tentacle/dev/Dockerfile";

            var tag = $"{hostPrefix}octopusdeploy/kubernetes-agent-tentacle:{FullSemVer}";

            if (!string.IsNullOrEmpty(tagSuffix))
            {
                tag += $"-{tagSuffix}";
            }

            if (includeDebugger)
                tag += "-debug";

            settings = settings
                .AddBuildArg($"BUILD_NUMBER={FullSemVer}", $"BUILD_DATE={DateTime.UtcNow:O}", $"RuntimeDepsTag={runtimeDepsImageTag}")
                .SetPlatform(DockerPlatform)
                .SetTag(tag)
                .SetFile(dockerfile)
                .SetPath(RootDirectory)
                .SetPush(push)
                .SetLoad(load);

            if (includeDebugger)
            {
                var debuggerArch = DockerPlatform.Replace("/", "-").Replace("amd", "x");
                settings = settings.AddBuildArg($"DEBUGGER_ARCH={debuggerArch}");
            }

            return settings;
        });
    }

    void CopyDebianPackageToDockerFolder(string runtimeId)
    {
        if (!DebDockerMap.TryGetValue(runtimeId, out var debDockerArch)) return;

        var (debArch, dockerArch) = debDockerArch;

        var packageFilePath = ArtifactsDirectory / "deb" / $"tentacle_{FullSemVer}_{debArch}.deb";

        var dockerDir = ArtifactsDirectory / "docker";
        dockerDir.CreateDirectory();

        packageFilePath.Copy(dockerDir / $"tentacle_{FullSemVer}_linux-{dockerArch}.deb");
    }

    string GetMicrok8sIpAddress()
    {
        var microk8sInfoOutput = Multipass.Invoke("info microk8s-vm --format json");
        var microk8sIp = microk8sInfoOutput.StdToJson()["info"]?["microk8s-vm"]?["ipv4"]?[0]?.ToString() ?? "localhost";
        return microk8sIp;
    }

    static IReadOnlyDictionary<string, (string deb, string docker)> DebDockerMap { get; } = new Dictionary<string, (string deb, string docker)>
    {
        { "linux-x64", ("amd64", "amd64") },
        { "linux-arm64", ("arm64", "arm64") },
        { "linux-arm", ("armhf", "armv7") }
    };

    AbsolutePath InstallOctopusCli()
    {
        const string cliVersion = "2.11.0";

        // Windows uses octopus.exe, everything else uses octopus
        var cliName = EnvironmentInfo.IsWin ? "octopus.exe" : "octopus";

        var unversionedCliFolder = TemporaryDirectory / "octopus-cli";
        var cliFolder = unversionedCliFolder / cliVersion;
        var cliPath = cliFolder / cliName;
        if (cliPath.FileExists())
        {
            // Assume it has already been installed
            return cliPath;
        }

        cliFolder.CreateDirectory();

        var osId = true switch
        {
            _ when EnvironmentInfo.IsWin => "windows",
            _ when EnvironmentInfo.IsOsx => "macOS",
            _ when EnvironmentInfo.IsLinux => "linux",
            _ => throw new NotSupportedException("Unsupported OS")
        };

        var archId = EnvironmentInfo.IsArm64
            ? "arm64"
            : "amd64";

        var archiveExtension = EnvironmentInfo.IsWin ? "zip" : "tar.gz";

        var downloadUri = $"https://github.com/OctopusDeploy/cli/releases/download/v{cliVersion}/octopus_{cliVersion}_{osId}_{archId}.{archiveExtension}";

        var archiveDestination = unversionedCliFolder / $"octopus-cli-archive.{cliVersion}.{archiveExtension}";

        // If the archive already exists, we will download it again
        archiveDestination.DeleteFile();

        Log.Information("Downloading Octopus CLI from {downloadUri}", downloadUri);
        HttpTasks.HttpDownloadFile(downloadUri, archiveDestination);

        archiveDestination.UncompressTo(cliFolder);
        Assert.FileExists(cliPath, "The Octopus CLI executable was not found after extracting the archive");

        if (!EnvironmentInfo.IsWin)
        {
            // We need to make the file executable as Nuke doesn't do that for us
            ProcessTasks.StartProcess("chmod", $"+x {cliPath}").WaitForExit();
        }

        return cliPath;
    }
}
