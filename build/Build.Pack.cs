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
    const string KubernetesTentacleContainerRuntimeDepsTag = "8.0-bookworm-slim";
    
    //We don't sign linux packages when building locally
    readonly bool SignLinuxPackages = !IsLocalBuild;

    [Parameter("Used to set a custom docker builder when executing DockerBuildxBuild tasks",
        Name = "DockerBuilder")]
    string? DockerBuildxBuilder;

    [Parameter("Specifies the platforms to build the docker images in. Multiple platforms must be comma-separated. Defaults to 'linux/arm64,linux/amd64'.",
        Name = "DockerPlatform")]
    string DockerPlatform = "linux/arm64,linux/amd64";

    [PathVariable]
    readonly Tool Multipass = null!;

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
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, debBuildDir / "scripts"));

                DockerTasks.DockerPull(settings => settings
                    .When(RuntimeInformation.OSArchitecture == Architecture.Arm64, _ => _.SetPlatform("linux/amd64"))
                    .SetName(dockerToolsContainerImage));

                DockerTasks.DockerRun(settings => settings
                    .When(RuntimeInformation.OSArchitecture == Architecture.Arm64, _ => _.SetPlatform("linux/amd64"))
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
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, ArtifactsDirectory / "deb"));

                CopyDebianPackageToDockerFolder(runtimeId);

                (ArtifactsDirectory / "rpm").CreateDirectory();
                debOutputDirectory.GlobFiles("*.rpm")
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, ArtifactsDirectory / "rpm"));
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
            .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingTentacleDirectory));

        ZipFile.CreateFromDirectory(
            workingDirectory,
            ArtifactsDirectory / "zip" / $"tentacle-{FullSemVer}-{framework}-{runtimeId}.zip");
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
                        .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));

                    (BuildDirectory / "Octopus.Manager.Tentacle" / framework / "win").GlobFiles("*")
                        .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));
                }
                else if (framework is NetCoreWindows)
                {
                    (BuildDirectory / "Tentacle" / framework / $"win-{platform}").GlobFiles("*")
                        .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));
                    
                    (BuildDirectory / "Octopus.Manager.Tentacle" / framework / $"win-{platform}").GlobFiles("*")
                        .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));
                }
                else
                {
                    (BuildDirectory / "Tentacle" / framework / $"win-{platform}").GlobFiles("*")
                        .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));
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

                    var platformString = framework switch
                    {
                        NetFramework => platform == MSBuildTargetPlatform.x64 ? "-x64" : "",
                        NetCoreWindows => $"-{framework}-win" + (platform == MSBuildTargetPlatform.x64 ? "-x64" : "-x86"),
                        _ => $"-{framework}-win" + (platform == MSBuildTargetPlatform.x64 ? "-x64" : "-x86")
                    };

                    FileSystemTasks.MoveFile(
                        builtMsi,
                        ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}{platformString}.msi");
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
            PackChocolateyPackageToArtifactsDirectory("", "-x64", chocolateyNetFrameworkSourceDirectory, chocolateyNetFrameworkNuspecFileName);
            
            var chocolateySelfContainedSourceDirectory = SourceDirectory / "Chocolatey-Self-Contained";
            const string chocolateySelfContainedNuspecFileName = "OctopusDeploy.Tentacle.SelfContained.nuspec";
            PackChocolateyPackageToArtifactsDirectory("-net8.0-windows-win-x86", "-net8.0-windows-win-x64", chocolateySelfContainedSourceDirectory, chocolateySelfContainedNuspecFileName);
        });

    void PackChocolateyPackageToArtifactsDirectory(string x86FileSuffix, string x64FileSuffix, AbsolutePath chocolateySourceDirectory, string pathToChocolateyNuspec)
    {
        var md5ChecksumNetFramework = (ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}{x86FileSuffix}.msi").GetFileHash();
        Log.Information($"MD5 Checksum: Octopus.Tentacle{x86FileSuffix}.msi = {md5ChecksumNetFramework}");

        var md5ChecksumX64NetFramework = (ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}{x64FileSuffix}.msi").GetFileHash();
        Log.Information($"Checksum: Octopus.Tentacle{x64FileSuffix}.msi = {md5ChecksumX64NetFramework}");

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

            var debAmd64PackageFilename = ConstructDebianPackageFilename("tentacle", "amd64");
            var debArm64PackageFilename = ConstructDebianPackageFilename("tentacle", "arm64");
            var debArm32PackageFilename = ConstructDebianPackageFilename("tentacle", "armhf");

            var rpmArm64PackageFilename = ConstructRedHatPackageFilename("tentacle", "aarch64");
            var rpmArm32PackageFilename = ConstructRedHatPackageFilename("tentacle", "armv7hl");
            var rpmx64PackageFilename = ConstructRedHatPackageFilename("tentacle", "x86_64");

            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}.msi", workingDirectory / "Octopus.Tentacle.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-x64.msi", workingDirectory / "Octopus.Tentacle-x64.msi");

            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-net8.0-win-x86.msi", workingDirectory / "Octopus.Tentacle-net8.0-win-x86.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-net8.0-win-x64.msi", workingDirectory / "Octopus.Tentacle-net8.0-win-x64.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-net8.0-windows-win-x86.msi", workingDirectory / "Octopus.Tentacle-net8.0-windows-win-x86.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-net8.0-windows-win-x64.msi", workingDirectory / "Octopus.Tentacle-net8.0-windows-win-x64.msi");

            FileSystemTasks.CopyFile(BuildDirectory / "Octopus.Tentacle.Upgrader" / NetCore / "win-x86" / "Octopus.Tentacle.Upgrader.exe", workingDirectory / "Octopus.Tentacle.Upgrader-net8.0-win-x86.exe");
            FileSystemTasks.CopyFile(BuildDirectory / "Octopus.Tentacle.Upgrader" / NetCore / "win-x64" / "Octopus.Tentacle.Upgrader.exe", workingDirectory / "Octopus.Tentacle.Upgrader-net8.0-win-x64.exe");

            var octopusTentacleUpgraderDirectory = BuildDirectory / "Octopus.Tentacle.Upgrader" / NetFramework / "win";
            octopusTentacleUpgraderDirectory.GlobFiles("*").ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingDirectory));
            FileSystemTasks.CopyFile(ArtifactsDirectory / "deb" / debAmd64PackageFilename, workingDirectory / debAmd64PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "deb" / debArm64PackageFilename, workingDirectory / debArm64PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "deb" / debArm32PackageFilename, workingDirectory / debArm32PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "rpm" / rpmArm64PackageFilename, workingDirectory / rpmArm64PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "rpm" / rpmArm32PackageFilename, workingDirectory / rpmArm32PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "rpm" / rpmx64PackageFilename, workingDirectory / rpmx64PackageFilename);

            foreach (var framework in new[] { NetFramework, NetCore })
            {
                foreach (var runtimeId in RuntimeIds)
                {
                    if (runtimeId == "win" && framework != NetFramework
                        || runtimeId != "win" && framework == NetFramework) continue;

                    var fileExtension = runtimeId.StartsWith("win") ? "zip" : "tar.gz";
                    FileSystemTasks.CopyFile(ArtifactsDirectory / "zip" / $"tentacle-{FullSemVer}-{framework}-{runtimeId}.{fileExtension}",
                        workingDirectory / $"tentacle-{framework}-{runtimeId}.{fileExtension}");
                }
            }
            
            FileSystemTasks.CopyFile(ArtifactsDirectory / "zip" / $"tentacle-{FullSemVer}-{NetCoreWindows}-win-x86.zip",
                workingDirectory / $"tentacle-{NetCoreWindows}-win-x86.zip");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "zip" / $"tentacle-{FullSemVer}-{NetCoreWindows}-win-x64.zip",
                workingDirectory / $"tentacle-{NetCoreWindows}-win-x64.zip");

            Assert.True((workingDirectory / "Octopus.Tentacle.msi").FileExists(), "Missing Octopus.Tentacle.msi");
            Assert.True((workingDirectory / "Octopus.Tentacle-x64.msi").FileExists(), "Missing Octopus.Tentacle-x64.msi");
            Assert.True((workingDirectory / "Octopus.Tentacle.Upgrader.exe").FileExists(), "Missing Octopus.Tentacle.Upgrader.exe");
            foreach (var framework in new[] {NetCore})
            {
                Assert.True((workingDirectory / $"Octopus.Tentacle-{framework}-win-x86.msi").FileExists(), $"Missing Octopus.Tentacle-{framework}-win-x86.msi");
                Assert.True((workingDirectory / $"Octopus.Tentacle-{framework}-win-x64.msi").FileExists(), $"Missing Octopus.Tentacle-{framework}-win-x64.msi");
                Assert.True((workingDirectory / $"Octopus.Tentacle-{framework}-windows-win-x86.msi").FileExists(), $"Missing Octopus.Tentacle-{framework}-windows-win-x86.msi");
                Assert.True((workingDirectory / $"Octopus.Tentacle-{framework}-windows-win-x64.msi").FileExists(), $"Missing Octopus.Tentacle-{framework}-windows-win-x64.msi");
                Assert.True((workingDirectory / $"Octopus.Tentacle.Upgrader-{framework}-win-x86.exe").FileExists(), $"Missing Octopus.Tentacle.Upgrader-{framework}-win-x86.exe");
                Assert.True((workingDirectory / $"Octopus.Tentacle.Upgrader-{framework}-win-x64.exe").FileExists(), $"Missing Octopus.Tentacle.Upgrader-{framework}-win-x64.exe");
            }
            Assert.True((workingDirectory / debAmd64PackageFilename).FileExists(), $"Missing {debAmd64PackageFilename}");
            Assert.True((workingDirectory / debArm64PackageFilename).FileExists(), $"Missing {debArm64PackageFilename}");
            Assert.True((workingDirectory / debArm32PackageFilename).FileExists(), $"Missing {debArm32PackageFilename}");
            Assert.True((workingDirectory / rpmArm64PackageFilename).FileExists(), $"Missing {rpmArm64PackageFilename}");
            Assert.True((workingDirectory / rpmArm32PackageFilename).FileExists(), $"Missing {rpmArm32PackageFilename}");
            Assert.True((workingDirectory / rpmx64PackageFilename).FileExists(), $"Missing {rpmx64PackageFilename}");

            const string description = "The deployment agent that is installed on each machine you plan to deploy to using Octopus.";
            const string author = "Octopus Deploy";
            const string title = "Octopus Tentacle cross platform bundle";

            const string id = "Octopus.Tentacle.CrossPlatformBundle";
            var outFolder = ArtifactsDirectory / "nuget";

            var octopus = InstallOctopusCli();
            // Note: Nuke automatically escapes this string by using the string interpolation syntax
            ProcessTasks.StartProcess(
                octopus,
                $"package nuget create --id {id} --version {FullSemVer} --base-path {workingDirectory} --out-folder {outFolder} --author {author} --title {title} --description {description} --no-prompt"
            ).WaitForExit();
        });

    [PublicAPI]
    Target Pack => _ => _
        .Description("Pack all the artifacts. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(PackCrossPlatformBundle)
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
            .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingDir / "tentacle"));
        tentacleDirectory.GlobFiles("*")
            .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingDir / "tentacle"));

        TarGZipCompress(
            workingDir,
            "tentacle",
            ArtifactsDirectory / "zip",
            $"tentacle-{FullSemVer}-{framework}-{runtimeId}.tar.gz");
    }

    void BuildAndPushOrLoadKubernetesTentacleContainerImage(bool push, bool load, string runtimeDepsImageTag, string? host = null,  bool includeDebugger = false, string? tagSuffix = null)
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

        FileSystemTasks.CopyFile(packageFilePath, dockerDir / $"tentacle_{FullSemVer}_linux-{dockerArch}.deb");
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
