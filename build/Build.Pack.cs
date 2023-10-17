// ReSharper disable RedundantUsingDirective

using System;
using System.IO.Compression;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Chocolatey;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build
{
    [PublicAPI]
    Target PackOsxTarballs => _ => _
        .Description("Packs the OS/X tarballs containing the published binaries.")
        .DependsOn(BuildOsx)
        .Executes(() =>
        {
            RuntimeIds.Where(x => x.StartsWith("osx-")).ForEach(PackTarballs);
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
            RuntimeIds.Where(x => x.StartsWith("linux-")).ForEach(PackTarballs);
        });

    [PublicAPI]
    Target PackLinuxPackagesLegacy => _ => _
        .Description("Legacy task until we can split creation of .rpm and .deb packages into their own tasks")
        .DependsOn(PackLinuxTarballs)
        .Requires(
            () => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PRIVATE_KEY")),
            () => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PASSPHRASE")))
        .Executes(() =>
        {
            const string dockerToolsContainerImage = "docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest";

            void CreateLinuxPackages(string runtimeId)
            {
                //TODO It's probable that the .deb and .rpm package layouts will be different - and potentially _should already_ be different.
                // We're approaching this with the assumption that we'll split .deb and .rpm creation soon, which means that we'll create a separate
                // filesystem layout for each of them. Using .deb for now; expecting to replicate that soon for .rpm.
                var debBuildDir = BuildDirectory / "deb" / runtimeId;
                FileSystemTasks.EnsureExistingDirectory(debBuildDir / "scripts");
                FileSystemTasks.EnsureExistingDirectory(debBuildDir / "output");

                var packagingScriptsDirectory = RootDirectory / "linux-packages" / "packaging-scripts";
                packagingScriptsDirectory.GlobFiles("*")
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, debBuildDir / "scripts"));

                DockerTasks.DockerPull(settings => settings
                    .SetName(dockerToolsContainerImage));

                DockerTasks.DockerRun(settings => settings
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
                        $"{BuildDirectory / "zip" / "net6.0" / runtimeId / "tentacle"}:/input",
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

                FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "deb");
                debOutputDirectory.GlobFiles("*.deb")
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, ArtifactsDirectory / "deb"));

                FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "rpm");
                debOutputDirectory.GlobFiles("*.rpm")
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, ArtifactsDirectory / "rpm"));
            }
        });

    [PublicAPI]
    Target PackDebianPackage => _ => _
        .Description("TODO: Move .deb creation into this task")
        .DependsOn(PackLinuxPackagesLegacy);

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
    Target PackWindowsZips => _ => _
        .Description("Packs the Windows .zip files containing the published binaries.")
        .DependsOn(BuildWindows)
        .Executes(() =>
        {
            FileSystemTasks.EnsureCleanDirectory(ArtifactsDirectory / "zip");

            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("win")))
            {
                var framework = runtimeId.Equals("win") ? NetFramework : NetCore;

                var workingDirectory = BuildDirectory / "zip" / framework / runtimeId;
                var workingTentacleDirectory = workingDirectory / "tentacle";

                FileSystemTasks.EnsureCleanDirectory(workingDirectory);
                FileSystemTasks.EnsureCleanDirectory(workingTentacleDirectory);

                (BuildDirectory / "Tentacle" / framework / runtimeId).GlobFiles($"*")
                    .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingTentacleDirectory));

                ZipFile.CreateFromDirectory(
                    workingDirectory,
                    ArtifactsDirectory / "zip" / $"tentacle-{FullSemVer}-{framework}-{runtimeId}.zip");
            }
        });

    [PublicAPI]
    Target PackWindowsInstallers => _ => _
        .Description("Packs the Windows .msi files.")
        .DependsOn(BuildWindows)
        .Executes(() =>
        {
            void PackWindowsInstallers(MSBuildTargetPlatform platform, AbsolutePath wixNugetPackagePath, string framework, string frameworkName)
            {
                var installerDirectory = BuildDirectory / "Installer";
                FileSystemTasks.EnsureExistingDirectory(installerDirectory);
                FileSystemTasks.EnsureCleanDirectory(installerDirectory);
                
                if (framework != NetCore)
                {
                    (BuildDirectory / "Tentacle" / framework / "win").GlobFiles("*")
                        .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));

                    (BuildDirectory / "Octopus.Manager.Tentacle" / framework / "win").GlobFiles("*")
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
                Logging.InBlock($"Building {platform} installer", () =>
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

                    string platformString;
                    if (framework == NetFramework)
                    {
                        platformString = platform == MSBuildTargetPlatform.x64 ? "-x64" : "";
                    }
                    else
                    {
                        platformString = $"-{NetCore}-win" + (platform == MSBuildTargetPlatform.x64 ? "-x64" : "-x86");
                    }

                    FileSystemTasks.MoveFile(
                        builtMsi,
                        ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}{platformString}.msi");
                });
            }

            // This is a slow operation
            var wixNugetInstalledPackage = NuGetPackageResolver.GetLocalInstalledPackage("wix", ToolPathResolver.NuGetPackagesConfigFile);
            if (wixNugetInstalledPackage == null) throw new Exception("Failed to find wix nuget package path");

            FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "msi");
            
            PackWindowsInstallers(MSBuildTargetPlatform.x64, wixNugetInstalledPackage.Directory, NetFramework, "NetFramework");
            PackWindowsInstallers(MSBuildTargetPlatform.x86, wixNugetInstalledPackage.Directory, NetFramework, "NetFramework");

            PackWindowsInstallers(MSBuildTargetPlatform.x64, wixNugetInstalledPackage.Directory, NetCore, "NetCore");
            PackWindowsInstallers(MSBuildTargetPlatform.x86, wixNugetInstalledPackage.Directory, NetCore, "NetCore");
        });

    [PublicAPI]
    Target PackChocolateyPackage => _ => _
        .Description("Packs the Chocolatey installer.")
        .DependsOn(PackWindowsInstallers)
        .Executes(() =>
        {
            FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "chocolatey");

            var md5Checksum = FileSystemTasks.GetFileHash(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}.msi");
            Log.Information($"MD5 Checksum: Octopus.Tentacle.msi = {md5Checksum}");

            var md5ChecksumX64 = FileSystemTasks.GetFileHash(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-x64.msi");
            Log.Information($"Checksum: Octopus.Tentacle-x64.msi = {md5ChecksumX64}");

            var chocolateyInstallScriptPath = SourceDirectory / "Chocolatey" / "chocolateyInstall.ps1";
            using var chocolateyInstallScriptFile = new ModifiableFileWithRestoreContentsOnDispose(chocolateyInstallScriptPath);

            chocolateyInstallScriptFile.ReplaceRegexInFiles("0.0.0", FullSemVer);
            chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksum>", md5Checksum);
            chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksumtype>", "md5");
            chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksum64>", md5ChecksumX64);
            chocolateyInstallScriptFile.ReplaceRegexInFiles("<checksumtype64>", "md5");

            ChocolateyTasks.ChocolateyPack(settings => settings
                .SetPathToNuspec(SourceDirectory / "Chocolatey" / "OctopusDeploy.Tentacle.nuspec")
                .SetVersion(NuGetVersion)
                .SetOutputDirectory(ArtifactsDirectory / "chocolatey"));
        });

    [PublicAPI]
    Target PackContracts => _ => _
        .Description("Packs the NuGet package for Tentacle contracts.")
        .Executes(() =>
        {
            FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "nuget");
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();

            DotNetPack(p => p
                .SetProject(RootDirectory / "source" / "Octopus.Tentacle.Contracts" / "Octopus.Tentacle.Contracts.csproj")
                .SetVersion(FullSemVer)
                .SetOutputDirectory(ArtifactsDirectory / "nuget")
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.Normal)
                .SetProperty("NuspecProperties", $"Version={FullSemVer}"));
        });

    [PublicAPI]
    Target PackClient => _ => _
        .Description("Packs the NuGet package for Tentacle Client.")
        .Executes(() =>
        {
            FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "nuget");
            using var versionInfoFile = ModifyTemplatedVersionAndProductFilesWithValues();

            DotNetPack(p => p
                .SetProject(RootDirectory / "source" / "Octopus.Tentacle.Client" / "Octopus.Tentacle.Client.csproj")
                .SetVersion(FullSemVer)
                .SetOutputDirectory(ArtifactsDirectory / "nuget")
                .DisableIncludeSymbols()
                .SetVerbosity(DotNetVerbosity.Normal)
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

            FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "nuget");

            var workingDirectory = BuildDirectory / "Octopus.Tentacle.CrossPlatformBundle";
            FileSystemTasks.EnsureExistingDirectory(workingDirectory);

            var debAmd64PackageFilename = ConstructDebianPackageFilename("tentacle", "amd64");
            var debArm64PackageFilename = ConstructDebianPackageFilename("tentacle", "arm64");
            var debArm32PackageFilename = ConstructDebianPackageFilename("tentacle", "armhf");

            var rpmArm64PackageFilename = ConstructRedHatPackageFilename("tentacle", "aarch64");
            var rpmArm32PackageFilename = ConstructRedHatPackageFilename("tentacle", "armv7hl");
            var rpmx64PackageFilename = ConstructRedHatPackageFilename("tentacle", "x86_64");

            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}.msi", workingDirectory / "Octopus.Tentacle.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-x64.msi", workingDirectory / "Octopus.Tentacle-x64.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-net6.0-win-x86.msi", workingDirectory / "Octopus.Tentacle-net6.0-win-x86.msi");
            FileSystemTasks.CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{FullSemVer}-net6.0-win-x64.msi", workingDirectory / "Octopus.Tentacle-net6.0-win-x64.msi");

            FileSystemTasks.CopyFile(BuildDirectory / "Octopus.Tentacle.Upgrader" / NetCore / "win-x86" / "Octopus.Tentacle.Upgrader.exe", workingDirectory / "Octopus.Tentacle.Upgrader-net6.0-win-x86.exe");
            FileSystemTasks.CopyFile(BuildDirectory / "Octopus.Tentacle.Upgrader" / NetCore / "win-x64" / "Octopus.Tentacle.Upgrader.exe", workingDirectory / "Octopus.Tentacle.Upgrader-net6.0-win-x64.exe");
            
            var octopusTentacleUpgraderDirectory = BuildDirectory / "Octopus.Tentacle.Upgrader" / NetFramework / "win";
            octopusTentacleUpgraderDirectory.GlobFiles("*").ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingDirectory));
            FileSystemTasks.CopyFile(ArtifactsDirectory / "deb" / debAmd64PackageFilename, workingDirectory / debAmd64PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "deb" / debArm64PackageFilename, workingDirectory / debArm64PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "deb" / debArm32PackageFilename, workingDirectory / debArm32PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "rpm" / rpmArm64PackageFilename, workingDirectory / rpmArm64PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "rpm" / rpmArm32PackageFilename, workingDirectory / rpmArm32PackageFilename);
            FileSystemTasks.CopyFile(ArtifactsDirectory / "rpm" / rpmx64PackageFilename, workingDirectory / rpmx64PackageFilename);

            foreach (var framework in new[] {NetFramework, NetCore})
            {
                foreach (var runtimeId in RuntimeIds)
                {
                    if (runtimeId == "win" && framework != "net48"
                        || runtimeId != "win" && framework == "net48") continue;

                    var fileExtension = runtimeId.StartsWith("win") ? "zip" : "tar.gz";
                    FileSystemTasks.CopyFile(ArtifactsDirectory / "zip" / $"tentacle-{FullSemVer}-{framework}-{runtimeId}.{fileExtension}",
                        workingDirectory / $"tentacle-{framework}-{runtimeId}.{fileExtension}");
                }
            }

            Assert.True((workingDirectory / "Octopus.Tentacle.msi").FileExists(), "Missing Octopus.Tentacle.msi");
            Assert.True((workingDirectory / "Octopus.Tentacle-x64.msi").FileExists(), "Missing Octopus.Tentacle-x64.msi");
            Assert.True((workingDirectory / "Octopus.Tentacle-net6.0-win-x86.msi").FileExists(), "Missing Octopus.Tentacle-net6.0-win-x86.msi");
            Assert.True((workingDirectory / "Octopus.Tentacle-net6.0-win-x64.msi").FileExists(), "Missing Octopus.Tentacle-net6.0-win-x64.msi");
            Assert.True((workingDirectory / "Octopus.Tentacle.Upgrader.exe").FileExists(), "Missing Octopus.Tentacle.Upgrader.exe");
            Assert.True((workingDirectory / "Octopus.Tentacle.Upgrader-net6.0-win-x86.exe").FileExists(), "Missing Octopus.Tentacle.Upgrader-net6.0-win-x86.exe");
            Assert.True((workingDirectory / "Octopus.Tentacle.Upgrader-net6.0-win-x64.exe").FileExists(), "Missing Octopus.Tentacle.Upgrader-net6.0-win-x64.exe");
            Assert.True((workingDirectory / debAmd64PackageFilename).FileExists(), $"Missing {debAmd64PackageFilename}");
            Assert.True((workingDirectory / debArm64PackageFilename).FileExists(), $"Missing {debArm64PackageFilename}");
            Assert.True((workingDirectory / debArm32PackageFilename).FileExists(), $"Missing {debArm32PackageFilename}");
            Assert.True((workingDirectory / rpmArm64PackageFilename).FileExists(), $"Missing {rpmArm64PackageFilename}");
            Assert.True((workingDirectory / rpmArm32PackageFilename).FileExists(), $"Missing {rpmArm32PackageFilename}");
            Assert.True((workingDirectory / rpmx64PackageFilename).FileExists(), $"Missing {rpmx64PackageFilename}");

            const string description = "The deployment agent that is installed on each machine you plan to deploy to using Octopus.";
            const string author = "Octopus Deploy";
            const string title = "Octopus Tentacle cross platform bundle";
            OctoCliTool($@"pack --id=Octopus.Tentacle.CrossPlatformBundle --version={FullSemVer} --basePath={workingDirectory} --outFolder={ArtifactsDirectory / "nuget"} --author=""{author}"" --title=""{title}"" --description=""{description}""");
        });

    [PublicAPI]
    Target Pack => _ => _
        .Description("Pack all the artifacts. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(PackCrossPlatformBundle)
        .DependsOn(PackContracts)
        .DependsOn(PackClient);

    void PackTarballs(string runtimeId)
    {
        FileSystemTasks.EnsureExistingDirectory(ArtifactsDirectory / "zip");

        var workingDir = BuildDirectory / "zip" / NetCore / runtimeId;
        FileSystemTasks.EnsureExistingDirectory(workingDir / "tentacle");

        var linuxPackagesContent = RootDirectory / "linux-packages" / "content";
        var tentacleDirectory = BuildDirectory / "Tentacle" / NetCore / runtimeId;

        linuxPackagesContent.GlobFiles("*")
            .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingDir / "tentacle"));
        tentacleDirectory.GlobFiles("*")
            .ForEach(x => FileSystemTasks.CopyFileToDirectory(x, workingDir / "tentacle"));

        TarGZipCompress(
            workingDir,
            "tentacle",
            ArtifactsDirectory / "zip",
            $"tentacle-{FullSemVer}-{NetCore}-{runtimeId}.tar.gz");
    }
}