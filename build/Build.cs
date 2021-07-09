using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Xml;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using Nuke.OctoVersion;
using OctoVersion.Core;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
// ReSharper disable StringLiteralTypo

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Default);

    [Solution] readonly Solution Solution = null!;
    [NukeOctoVersion] readonly OctoVersionInfo OctoVersionInfo = null!;

    [Parameter] string TestFramework = "";
    [Parameter] string TestRuntime = "";
    
    [PackageExecutable(
        packageId: "azuresigntool",
        packageExecutable: "azuresigntool.dll")]
    readonly Tool AzureSignTool = null!;
    
    [PackageExecutable(
        packageId: "wix",
        packageExecutable: "heat.exe")]
    readonly Tool WiXHeatTool = null!;

    [PackageExecutable(
        packageId: "chocolatey",
        packageExecutable: "chocolatey.exe")]
    readonly Tool ChocolateyTool = null!;

    [PackageExecutable(
        packageId: "OctopusTools",
        packageExecutable: "octo.exe")]
    readonly Tool OctoCliTool = null!;

    [Parameter] string AzureKeyVaultUrl = "";
    [Parameter] string AzureKeyVaultAppId = "";
    [Parameter] string AzureKeyVaultAppSecret = "";
    [Parameter] string AzureKeyVaultCertificateName = "";
    
    [Parameter(Name = "signing_certificate_path")] string SigningCertificatePath = RootDirectory / "certificates" / "OctopusDevelopment.pfx";
    [Parameter(Name = "signing_certificate_password")] string SigningCertificatePassword = "Password01!";

    readonly AbsolutePath SourceDirectory = RootDirectory / "source";
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "_artifacts";
    readonly AbsolutePath BuildDirectory = RootDirectory / "_build";
    readonly AbsolutePath LocalPackagesDirectory = RootDirectory / ".." / "LocalPackages";
    readonly AbsolutePath TestDirectory = RootDirectory / "_test";
    
    const string NetFramework = "net452";
    const string NetCore = "netcoreapp3.1";
    readonly string[] RuntimeIds = { "win", "win-x86", "win-x64", "linux-x64", "linux-musl-x64", "linux-arm64", "linux-arm", "osx-x64" };

    // Keep this list in order by most likely to succeed
    readonly string[] SigningTimestampUrls = {
        "http://timestamp.digicert.com?alg=sha256",
        "http://timestamp.comodoca.com"
    };

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj", "**/TestResults").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
            EnsureCleanDirectory(BuildDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    #region Build
    Target BuildWindows => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            static bool HasAuthenticodeSignature(AbsolutePath fileInfo)
            {
                // note: Doesn't check if existing signatures are valid, only that one exists
                // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
                try
                {
                    X509Certificate.CreateFromSignedFile(fileInfo);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            ModifyTemplatedVersionAndProductFilesWithValues(out var versionInfoRestoreAction, out var productWxsRestoreAction);
            foreach (var runtimeId in RuntimeIds)
            {
                if (runtimeId.StartsWith("win"))
                {
                    RunBuildFor(runtimeId.Equals("win") ? NetFramework : NetCore, runtimeId);
                }
            }
            
            versionInfoRestoreAction.Invoke();
            productWxsRestoreAction.Invoke();
            
            // Sign any unsigned libraries that Octopus Deploy authors so that they play nicely with security scanning tools.
            // Refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
            // Decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
            var windowsOnlyBuiltFileSpec = BuildDirectory.GlobDirectories($"**/win*/**");
        
            var filesToSign = windowsOnlyBuiltFileSpec
                .SelectMany(x => x.GlobFiles("**/Octo*.exe", "**/Octo*.dll", "**/Tentacle.exe", "**/Tentacle.dll", "**/Halibut.dll", "**/Nuget.*.dll"))
                .Where(file => !HasAuthenticodeSignature(file))
                .ToArray();

            SignAndTimeStamp(filesToSign);
        });

    Target BuildLinux => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            ModifyTemplatedVersionAndProductFilesWithValues(out var versionInfoRestoreAction, out var productWxsRestoreAction);

            foreach (var runtimeId in RuntimeIds)
            {
                if (runtimeId.StartsWith("linux-"))
                {
                    RunBuildFor(NetCore, runtimeId);
                }
            }
            
            versionInfoRestoreAction.Invoke();
            productWxsRestoreAction.Invoke();
        });

    // ReSharper disable once InconsistentNaming
    Target BuildOSX => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            ModifyTemplatedVersionAndProductFilesWithValues(out var versionInfoRestoreAction, out var productWxsRestoreAction);

            foreach (var runtimeId in RuntimeIds)
            {
                if (runtimeId.StartsWith("osx-"))
                {
                    RunBuildFor(NetCore, runtimeId);
                }
            }
            
            versionInfoRestoreAction.Invoke();
            productWxsRestoreAction.Invoke();
        });

    // ReSharper disable once UnusedMember.Local
    Target BuildAll => _ => _
        .Description("Build all the framework/runtime combinations. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(BuildWindows)
        .DependsOn(BuildLinux)
        .DependsOn(BuildOSX);
    #endregion

    #region Pack Targets
    Target PackWindowsZips => _ => _
        .Description("Packs the Windows .zip files containing the published binaries.")
        .DependsOn(BuildWindows)
        .Executes(() =>
        {
            EnsureCleanDirectory(ArtifactsDirectory / "zip");
            
            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("win")))
            {
                var framework = runtimeId.Equals("win") ? NetFramework : NetCore;
                
                var workingDirectory = BuildDirectory / "zip" / framework / runtimeId;
                var workingTentacleDirectory = workingDirectory / "tentacle";
    
                EnsureCleanDirectory(workingDirectory);
                EnsureCleanDirectory(workingTentacleDirectory);

                (BuildDirectory / "Tentacle" / framework / runtimeId).GlobFiles($"*")
                    .ForEach(x => CopyFileToDirectory(x, workingTentacleDirectory));
                
                ZipFile.CreateFromDirectory(
                    workingDirectory, 
                    ArtifactsDirectory / "zip" / $"tentacle-{OctoVersionInfo.FullSemVer}-{framework}-{runtimeId}.zip");
            }
        });

    Target PackLinuxTarballs => _ => _
        .Description("Packs the Linux tarballs containing the published binaries.")
        .DependsOn(BuildLinux)
        .Executes(() =>
        {
            EnsureExistingDirectory(ArtifactsDirectory / "zip");

            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("linux-")))
            {
                var workingDir = BuildDirectory / "zip" / NetCore / runtimeId;
                EnsureExistingDirectory(workingDir);
                EnsureExistingDirectory(workingDir / "tentacle");
                
                (RootDirectory / "linux-packages" / "content").GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, workingDir / "tentacle"));
                (BuildDirectory / "Tentacle" / NetCore / runtimeId).GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, workingDir / "tentacle"));
                TarGZipCompress(
                    workingDir,
                    "tentacle",
                    ArtifactsDirectory / "zip",
                    $"tentacle-{OctoVersionInfo.FullSemVer}-{NetCore}-{runtimeId}.tar.gz");
            }
        });

    // ReSharper disable once InconsistentNaming
    Target PackOSXTarballs => _ => _
        .Description("Packs the OS/X tarballs containing the published binaries.")
        .DependsOn(BuildOSX)
        .Executes(() =>
        {
            EnsureExistingDirectory(ArtifactsDirectory / "zip");

            foreach (var runtimeId in RuntimeIds.Where(x => x.StartsWith("osx-")))
            {
                var workingDir = BuildDirectory / "zip" / NetCore / runtimeId;
                EnsureExistingDirectory(workingDir);
                EnsureExistingDirectory(workingDir / "tentacle");

                (RootDirectory / "linux-packages" / "content").GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, workingDir / "tentacle"));
                (BuildDirectory / "Tentacle" / NetCore / runtimeId).GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, workingDir / "tentacle"));
                TarGZipCompress(
                    workingDir,
                    "tentacle",
                    ArtifactsDirectory / "zip",
                    $"tentacle-{OctoVersionInfo.FullSemVer}-{NetCore}-{runtimeId}.tar.gz");
            }
        });

    Target PackChocolateyPackage => _ => _
        .Description("Packs the Chocolatey installer.")
        .DependsOn(PackWindowsInstallers)
        .Executes(() =>
        {
            EnsureExistingDirectory(ArtifactsDirectory / "chocolatey");
            
            var md5Checksum = GetFileHash(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{OctoVersionInfo.FullSemVer}.msi");
            Logger.Info($"MD5 Checksum: Octopus.Tentacle.msi = {md5Checksum}");

            var md5ChecksumX64 = GetFileHash(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{OctoVersionInfo.FullSemVer}-x64.msi");
            Logger.Info($"Checksum: Octopus.Tentacle-x64.msi = {md5ChecksumX64}");

            var chocolateyInstallScriptPath = SourceDirectory / "Chocolatey" / "chocolateyInstall.ps1";
            var restoreChocolateyInstallScriptAction = RestoreFileForCleanup(chocolateyInstallScriptPath);

            try
            {
                ReplaceTextInFiles(chocolateyInstallScriptPath, "0.0.0", OctoVersionInfo.FullSemVer);
                ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum>", md5Checksum);
                ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype>", "md5"); 
                ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum64>", md5ChecksumX64);
                ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype64>", "md5");
            
                //Once PR merged used Chocolatey Task: https://github.com/nuke-build/nuke/pull/755

                var chocolateyArguments = $"pack {SourceDirectory / "Chocolatey" / "OctopusDeploy.Tentacle.nuspec"} " +
                    $"-version {OctoVersionInfo.NuGetVersion} " +
                    $"-outputDirectory {ArtifactsDirectory / "chocolatey"}";

                ChocolateyTool(chocolateyArguments);
            }
            finally
            {
                restoreChocolateyInstallScriptAction.Invoke();
            }
        });

    Target PackLinuxPackagesLegacy => _ => _
        .Description("Legacy task until we can split creation of .rpm and .deb packages into their own tasks")
        .DependsOn(PackLinuxTarballs)
        .Executes(() =>
        {
            void CreateLinuxPackages(string runtimeId)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PRIVATE_KEY")))
                {
                    throw new Exception("This build requires environment variables `SIGN_PRIVATE_KEY` (in a format gpg1 can import)"
                        + " and `SIGN_PASSPHRASE`, which are used to sign the .rpm.");
                }

                //TODO It's probable that the .deb and .rpm package layouts will be different - and potentially _should already_ be different.
                // We're approaching this with the assumption that we'll split .deb and .rpm creation soon, which means that we'll create a separate
                // filesystem layout for each of them. Using .deb for now; expecting to replicate that soon for .rpm.
                var debBuildDir = BuildDirectory / "deb" / runtimeId;
                EnsureExistingDirectory(debBuildDir);
                EnsureExistingDirectory(debBuildDir / "scripts");
                EnsureExistingDirectory(debBuildDir / "output");

                (RootDirectory / "linux-packages" / "packaging-scripts").GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, debBuildDir / "scripts"));

                DockerTasks.DockerPull(settings => settings
                    .SetName("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest"));

                DockerTasks.DockerRun(settings => settings
                    .EnableRm()
                    .EnableTty()
                    .SetEnv(
                        $"VERSION={OctoVersionInfo.FullSemVer}",
                        "INPUT_PATH=/input",
                        "OUTPUT_PATH=/output",
                        "SIGN_PRIVATE_KEY",
                        "SIGN_PASSPHRASE")
                    .SetVolume( 
                        $"{debBuildDir / "scripts"}:/scripts",
                        $"{BuildDirectory / "zip" / "netcoreapp3.1" / runtimeId / "tentacle"}:/input",
                        $"{debBuildDir / "output"}:/output"
                    )
                    .SetImage("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest")
                    .SetCommand("bash")
                    .SetArgs("/scripts/package.sh", runtimeId));
            }

            var targetRuntimeIds = RuntimeIds.Where(x => x.StartsWith("linux-"))
                .Where(x => x != "linux-musl-x64"); // not supported yet. Work in progress.

            foreach (var runtimeId in targetRuntimeIds)
            {
                CreateLinuxPackages(runtimeId);
                
                EnsureExistingDirectory(ArtifactsDirectory / "deb");
                (BuildDirectory / "deb" / runtimeId / "output").GlobFiles("*.deb")
                    .ForEach(x => CopyFileToDirectory(x, ArtifactsDirectory / "deb"));

                EnsureExistingDirectory(ArtifactsDirectory / "rpm");
                (BuildDirectory / "deb" / runtimeId / "output").GlobFiles("*.rpm")
                    .ForEach(x => CopyFileToDirectory(x, ArtifactsDirectory / "rpm"));
            }
        });

    Target PackDebianPackage => _ => _
        .Description("TODO: Move .deb creation into this task")
        .DependsOn(PackLinuxPackagesLegacy);

    Target PackRedHatPackage => _ => _
        .Description("TODO: Move .rpm creation into this task")
        .DependsOn(PackLinuxPackagesLegacy);

    Target PackWindowsInstallers => _ => _
        .Description("Packs the Windows .msi files.")
        .DependsOn(BuildWindows)
        .Executes(() =>
        {
            void PackWindowsInstallers(MSBuildTargetPlatform platform, AbsolutePath wixNugetPackagePath)
            {
                var installerDirectory = BuildDirectory / "Installer";
                EnsureExistingDirectory(installerDirectory);

                (BuildDirectory / "Tentacle" / NetFramework / "win").GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));
                (BuildDirectory / "Octopus.Manager.Tentacle" / NetFramework / "win").GlobFiles("*")
                    .ForEach(x => CopyFileToDirectory(x, installerDirectory, FileExistsPolicy.Overwrite));
                CopyFileToDirectory(RootDirectory / "scripts" / "Harden-InstallationDirectory.ps1", installerDirectory, FileExistsPolicy.Overwrite);

                var harvestFile = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Tentacle.Generated.wxs";
                var restoreHarvestFileAction = RestoreFileForCleanup(harvestFile);

                try
                {
                    GenerateMsiInstallerContents(installerDirectory, harvestFile);
                    BuildMsiInstallerForPlatform(platform, wixNugetPackagePath);
                }
                finally
                {
                    //cleanup harvest file
                    restoreHarvestFileAction.Invoke();
                }
            }
            
            void GenerateMsiInstallerContents(AbsolutePath installerDirectory, AbsolutePath harvestFile)
            {
                InBlock("Running HEAT to generate the installer contents...", () =>
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

            void BuildMsiInstallerForPlatform(MSBuildTargetPlatform platform, AbsolutePath wixNugetPackagePath)
            {
                InBlock($"Building {platform} installer", () =>
                {
                    var tentacleInstallerWixProject = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Octopus.Tentacle.Installer.wixproj";
                    var restoreWiXProjectAction = RestoreFileForCleanup(tentacleInstallerWixProject);
                    try
                    {
                        ReplaceTextInFiles(tentacleInstallerWixProject, "{WixToolPath}", wixNugetPackagePath / "tools");
                        ReplaceTextInFiles(tentacleInstallerWixProject, "{WixTargetsPath}", wixNugetPackagePath / "tools" / "Wix.targets");
                        ReplaceTextInFiles(tentacleInstallerWixProject, "{WixTasksPath}", wixNugetPackagePath / "tools" / "wixtasks.dll");
                        
                        MSBuildTasks.MSBuild(settings => settings
                            .SetConfiguration("Release")
                            .SetProperty("AllowUpgrade", "True")
                            .SetVerbosity(MSBuildVerbosity.Normal) //TODO: Set verbosity from command line argument
                            .SetTargets("build")
                            .SetTargetPlatform(platform)
                            .SetTargetPath(RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Octopus.Tentacle.Installer.wixproj"));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    finally
                    {
                        restoreWiXProjectAction.Invoke();
                    }
                    
                    var builtMsi = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "bin" / platform / "Octopus.Tentacle.msi";
                    SignAndTimeStamp(builtMsi);
                    
                    var platformString = platform == MSBuildTargetPlatform.x64 ? "-x64" : "";
                    MoveFile(
                        builtMsi,
                        ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{OctoVersionInfo.FullSemVer}{platformString}.msi");
                });
            }

            // This is a slow operation
            var wixNugetInstalledPackage = NuGetPackageResolver.GetLocalInstalledPackage("wix", ToolPathResolver.NuGetPackagesConfigFile);
            if (wixNugetInstalledPackage == null) throw new Exception("Failed to find wix nuget package path");

            EnsureExistingDirectory(ArtifactsDirectory / "msi");
            PackWindowsInstallers(MSBuildTargetPlatform.x64, wixNugetInstalledPackage.Directory);
            PackWindowsInstallers(MSBuildTargetPlatform.x86, wixNugetInstalledPackage.Directory);
        });

    // ReSharper disable InconsistentNaming
    Target PackCrossPlatformBundle => _ => _
        .Description("Packs the cross-platform Tentacle.nupkg used by Octopus Server to dynamically upgrade Tentacles.")
        // Disabling these dependencies right now until we understand how we can consume artifacts from previous build steps without having to re-run this target
        // .DependsOn(PackWindows)
        // .DependsOn(PackLinux)
        // .DependsOn(PackOSX)
        .Executes(() =>
        {
            string ConstructDebianPackageFilename(string packageName, string architecture) {
                var filename = $"{packageName}_{OctoVersionInfo.FullSemVer}_{architecture}.deb";
                return filename;
            }
            
            string ConstructRedHatPackageFilename(string packageName, string architecture) {
                var transformedVersion = OctoVersionInfo.FullSemVer.Replace("-", "_");
                var filename = $"{packageName}-{transformedVersion}-1.{architecture}.rpm";
                return filename;
            }
            
            EnsureExistingDirectory(ArtifactsDirectory / "nuget");

            var workingDirectory = BuildDirectory / "Octopus.Tentacle.CrossPlatformBundle";
            EnsureExistingDirectory(workingDirectory);

            var debAMD64PackageFilename = ConstructDebianPackageFilename("tentacle", "amd64");
            var debARM64PackageFilename = ConstructDebianPackageFilename("tentacle", "arm64");
            var debARM32PackageFilename = ConstructDebianPackageFilename("tentacle", "armhf");
            
            var rpmARM64PackageFilename = ConstructRedHatPackageFilename("tentacle", "aarch64");
            var rpmARM32PackageFilename = ConstructRedHatPackageFilename("tentacle", "armv7hl");
            // ReSharper disable once IdentifierTypo
            var rpmx64PackageFilename = ConstructRedHatPackageFilename("tentacle", "x86_64");
            
            CopyFileToDirectory(SourceDirectory / "Octopus.Tentacle.CrossPlatformBundle" / "Octopus.Tentacle.CrossPlatformBundle.nuspec", workingDirectory);
            CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{OctoVersionInfo.FullSemVer}.msi", workingDirectory / "Octopus.Tentacle.msi");
            CopyFile(ArtifactsDirectory / "msi" / $"Octopus.Tentacle.{OctoVersionInfo.FullSemVer}-x64.msi", workingDirectory / "Octopus.Tentacle-x64.msi");
            (BuildDirectory / "Octopus.Tentacle.Upgrader" / NetFramework / "win").GlobFiles("*").ForEach(x => CopyFileToDirectory(x, workingDirectory));
            CopyFile(ArtifactsDirectory / "deb" / debAMD64PackageFilename, workingDirectory / debAMD64PackageFilename);
            CopyFile(ArtifactsDirectory / "deb" / debARM64PackageFilename, workingDirectory / debARM64PackageFilename);
            CopyFile(ArtifactsDirectory / "deb" / debARM32PackageFilename, workingDirectory / debARM32PackageFilename);
            CopyFile(ArtifactsDirectory / "rpm" / rpmARM64PackageFilename, workingDirectory / rpmARM64PackageFilename);
            CopyFile(ArtifactsDirectory / "rpm" / rpmARM32PackageFilename, workingDirectory / rpmARM32PackageFilename);
            CopyFile(ArtifactsDirectory / "rpm" / rpmx64PackageFilename, workingDirectory / rpmx64PackageFilename);

            foreach (var framework in new[] {NetFramework, NetCore})
            {
                foreach (var runtimeId in RuntimeIds)
                {
                    if (runtimeId == "win" && framework != "net452"
                        || runtimeId != "win" && framework == "net452") continue;

                    var fileExtension = runtimeId.StartsWith("win") ? "zip" : "tar.gz";
                    CopyFile(ArtifactsDirectory / "zip" / $"tentacle-{OctoVersionInfo.FullSemVer}-{framework}-{runtimeId}.{fileExtension}",
                        workingDirectory / $"tentacle-{framework}-{runtimeId}.{fileExtension}");
                }
            }
            
            ControlFlow.Assert(FileExists(workingDirectory / "Octopus.Tentacle.msi"), "Missing Octopus.Tentacle.msi");
            ControlFlow.Assert(FileExists(workingDirectory / "Octopus.Tentacle-x64.msi"), "Missing Octopus.Tentacle-x64.msi");
            ControlFlow.Assert(FileExists(workingDirectory / "Octopus.Tentacle.Upgrader.exe"), "Missing Octopus.Tentacle.Upgrader.exe");
            ControlFlow.Assert(FileExists(workingDirectory / debAMD64PackageFilename), $"Missing {debAMD64PackageFilename}");
            ControlFlow.Assert(FileExists(workingDirectory / debARM64PackageFilename), $"Missing {debARM64PackageFilename}");
            ControlFlow.Assert(FileExists(workingDirectory / debARM32PackageFilename), $"Missing {debARM32PackageFilename}");
            ControlFlow.Assert(FileExists(workingDirectory / rpmARM64PackageFilename), $"Missing {rpmARM64PackageFilename}");
            ControlFlow.Assert(FileExists(workingDirectory / rpmARM32PackageFilename), $"Missing {rpmARM32PackageFilename}");
            ControlFlow.Assert(FileExists(workingDirectory / rpmx64PackageFilename), $"Missing {rpmx64PackageFilename}");

            OctoCliTool($"pack --id=Octopus.Tentacle.CrossPlatformBundle --version={OctoVersionInfo.FullSemVer} --basePath={workingDirectory} --outFolder={ArtifactsDirectory / "nuget"}");
        });

    // ReSharper disable once UnusedMember.Local
    Target PackWindows => _ => _
        .Description("Packs all the Windows targets.")
        .DependsOn(BuildWindows)
        .DependsOn(PackWindowsZips)
        .DependsOn(PackChocolateyPackage)
        .DependsOn(PackWindowsInstallers);

    // ReSharper disable once UnusedMember.Local
    Target PackLinux => _ => _
        .Description("Packs all the Linux targets.")
        .DependsOn(BuildLinux)
        .DependsOn(PackLinuxTarballs)
        .DependsOn(PackDebianPackage)
        .DependsOn(PackRedHatPackage);

    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once InconsistentNaming
    Target PackOSX => _ => _
        .Description("Packs all the OS/X targets.")
        .DependsOn(BuildOSX)
        .DependsOn(PackOSXTarballs);

    Target Pack => _ => _
        .Description("Pack all the artifacts. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(PackCrossPlatformBundle);
    #endregion
    
    #region tests
    // ReSharper disable once UnusedMember.Local
    Target TestWindows => _ => _
        .DependsOn(BuildWindows)
        .Executes(RunTests);
    
    // ReSharper disable once UnusedMember.Local
    Target TestLinux => _ => _
        .DependsOn(BuildLinux)
        .Executes(RunTests);
    
    // ReSharper disable once UnusedMember.Local
    Target TestOSX => _ => _
        .DependsOn(BuildOSX)
        .Executes(RunTests);

    // ReSharper disable once UnusedMember.Local
    Target TestWindowsInstallerPermissions => _ => _
        .DependsOn(PackWindowsInstallers)
        .Executes(() =>
        {
            string GetTestName(AbsolutePath installerPath) => Path.GetFileName(installerPath).Replace(".msi", "");
            
            void TestInstallerPermissions(AbsolutePath installerPath)
            {
                var destination = TestDirectory / "install" / GetTestName(installerPath);
                EnsureExistingDirectory(destination);

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
                
                Logger.Info($"BUILTIN\\Users do not have write access to {destination}. Hooray!");
            }

            void InstallMsi(AbsolutePath installerPath, AbsolutePath destination)
            {
                var installLogName = Path.Combine(TestDirectory, $"{GetTestName(installerPath)}.install.log");

                Logger.Info($"Installing {installerPath} to {destination}");

                var arguments = $"/i {installerPath} /QN INSTALLLOCATION={destination} /L*V {installLogName}";
                Logger.Info($"Running msiexec {arguments}");
                var installationProcess = ProcessTasks.StartProcess("msiexec", arguments);
                installationProcess.WaitForExit();
                CopyFileToDirectory(installLogName, ArtifactsDirectory);
                if (installationProcess.ExitCode != 0) {
                    throw new Exception($"The installation process exited with a non-zero exit code ({installationProcess.ExitCode}). Check the log {installLogName} for details.");
                }
            }
            
            void UninstallMsi(AbsolutePath installerPath)
            {
                Logger.Info($"Uninstalling {installerPath}");
                var uninstallLogName = Path.Combine(TestDirectory, $"{GetTestName(installerPath)}.uninstall.log");

                var arguments = $"/x {installerPath} /QN /L*V {uninstallLogName}";
                Logger.Info($"Running msiexec {arguments}");
                var uninstallProcess = ProcessTasks.StartProcess("msiexec", arguments);
                uninstallProcess.WaitForExit();
                CopyFileToDirectory(uninstallLogName, ArtifactsDirectory);
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

            EnsureExistingDirectory(TestDirectory);
            EnsureCleanDirectory(TestDirectory);

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
    #endregion

    Target CopyToLocalPackages => _ => _
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(PackCrossPlatformBundle)
        .Description("If not running on a build agent, this step copies the relevant built artifacts to the local packages cache.")
        .Executes(() =>
        {
            EnsureExistingDirectory(LocalPackagesDirectory);
            CopyFileToDirectory(ArtifactsDirectory / "Chocolatey" / $"OctopusDeploy.Tentacle.{OctoVersionInfo.NuGetVersion}.nupkg", LocalPackagesDirectory);
        });

    Target Default => _ => _
        .DependsOn(Pack)
        .DependsOn(CopyToLocalPackages);

    #region implementation details

    void RunTests()
    {
        Logger.Info($"Running test for Framework: {TestFramework} and Runtime: {TestRuntime}");

        DotNet("--info");
        
        EnsureExistingDirectory(ArtifactsDirectory / "teamcity");
            
        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var testAssembliesPath = (BuildDirectory / "Octopus.Tentacle.Tests" / TestFramework / TestRuntime)
            .GlobFiles("*.Tests.dll");
        var testResultsPath = ArtifactsDirectory / "teamcity" / $"TestResults-{TestFramework}-{TestRuntime}.xml";
        
        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            testAssembliesPath.ForEach(x =>
                DotNetTest(settings => settings
                    .SetProjectFile(x)
                    .SetFramework(TestFramework)
                    .SetLogger($"trx;LogFileName={testResultsPath}"))
            );
        }
        catch (Exception e)
        {
            Logger.Warn($"{e.Message}: {e}");
        }
    }

    static string DeriveGitBranch()
    {
        var branch = Environment.GetEnvironmentVariable("OCTOVERSION_CurrentBranch");
        if (string.IsNullOrEmpty(branch))
        {
            Logger.Error("Git branch not available from environment variable. This should be set via OctoVersion for local dev and defined in environment variable OCTOVERSION_CurrentBranch for build servers.");
            const string message = "Git branch not available from environment variable";
            if (TeamCity.Instance != null)
            {
                Console.WriteLine($"##teamcity[message text='{message}' status='FAILURE']");
            }
            throw new NotSupportedException(message);
        }

        return branch;
    }
    
    //Modifies the VersionInfo.cs and Product.wxs files to embed version information into the shipped product.
    void ModifyTemplatedVersionAndProductFilesWithValues(out Action versionInfoRestoreAction, out Action productWxsRestoreAction)
    {
        void UpdateMsiProductVersion(AbsolutePath productWxs)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(productWxs);

            var namespaceManager = new XmlNamespaceManager(xmlDoc.NameTable);
            namespaceManager.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");

            var product = xmlDoc.SelectSingleNode("//wi:Product", namespaceManager);

            if (product == null) throw new Exception("Couldn't find Product Node in wxs file");
            if (product.Attributes == null) throw new Exception("Couldn't find Version attribute in Product Node");

            // ReSharper disable once PossibleNullReferenceException
            product.Attributes["Version"]!.Value = OctoVersionInfo.MajorMinorPatch;

            xmlDoc.Save(productWxs);
        }

        static void ReplaceRegexInFiles(AbsolutePath file, string matchingPattern, string replacement)
        {
            var fileText = File.ReadAllText(file);
            fileText = Regex.Replace(fileText, matchingPattern, replacement);
            File.WriteAllText(file, fileText);
        }

        var versionInfoFile = SourceDirectory / "Solution Items" / "VersionInfo.cs";
        var productWxs = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Product.wxs";

        //TODO: Use this action
        versionInfoRestoreAction = RestoreFileForCleanup(versionInfoFile);
        productWxsRestoreAction = RestoreFileForCleanup(productWxs);

        ReplaceRegexInFiles(versionInfoFile, "AssemblyVersion\\(\".*?\"\\)", $"AssemblyVersion(\"{OctoVersionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyFileVersion\\(\".*?\"\\)", $"AssemblyFileVersion(\"{OctoVersionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyInformationalVersion\\(\".*?\"\\)", $"AssemblyInformationalVersion(\"{OctoVersionInfo.FullSemVer}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyGitBranch\\(\".*?\"\\)", $"AssemblyGitBranch(\"{DeriveGitBranch()}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyNuGetVersion\\(\".*?\"\\)", $"AssemblyNuGetVersion(\"{OctoVersionInfo.FullSemVer}\")");

        UpdateMsiProductVersion(productWxs);
    }
    
    void RunBuildFor(string framework, string runtimeId)
    {
        var configuration = $"Release-{framework}-{runtimeId}";
        
        DotNetPublish(p => p
            .SetProject(SourceDirectory / "Tentacle.sln")
            .SetConfiguration(configuration)
            .SetFramework(framework)
            .SetRuntime(runtimeId)
            .EnableNoRestore()
            .SetVersion(OctoVersionInfo.FullSemVer));
    }
    
    static void ReplaceTextInFiles(AbsolutePath path, string oldValue, string newValue)
    {
        var fileText = File.ReadAllText(path);
        fileText = fileText.Replace(oldValue, newValue);
        File.WriteAllText(path, fileText);
    }
    
    Action RestoreFileForCleanup(AbsolutePath file)
    {
        var contents = File.ReadAllBytes(file);
        return () => {
            Logger.Info("Restoring {0}", file);
            File.WriteAllBytes(file, contents);
        };
    }

    void SignAndTimeStamp(params AbsolutePath[] files)
    {
        InBlock("Signing and timestamping...", () =>
        {
            foreach (var file in files)
            {
                if (!FileExists(file)) throw new Exception($"File {file} does not exist");
                var fileInfo = new FileInfo(file);

                if (fileInfo.IsReadOnly)
                {
                    InBlock($"{file} is readonly. Making it writeable.", () => {
                        fileInfo.IsReadOnly = false;
                    });
                }
            }

            if (string.IsNullOrEmpty(AzureKeyVaultUrl)
                && string.IsNullOrEmpty(AzureKeyVaultAppId)
                && string.IsNullOrEmpty(AzureKeyVaultAppSecret)
                && string.IsNullOrEmpty(AzureKeyVaultCertificateName))
            { 
                Logger.Info("Signing files using signtool and the self-signed development code signing certificate.");
                SignWithSignTool(files);
            }
            else
            {
                Logger.Info("Signing files using azuresigntool and the production code signing certificate.");
                SignWithAzureSignTool(files);
            }
        });
    }

    void SignWithSignTool(AbsolutePath[] files)
    {
        var lastException = default(Exception);
        foreach (var timestampUrl in SigningTimestampUrls)
        {
            InBlock($"Trying to time stamp using {timestampUrl}", () =>
            {
                try
                {
                    SignToolTasks.SignTool(settings => settings
                        .SetFile(SigningCertificatePath)
                        .SetPassword(SigningCertificatePassword)
                        .SetFileDigestAlgorithm("sha256")
                        .SetRfc3161TimestampServerUrl(timestampUrl)
                        .SetProcessToolPath(RootDirectory / "signtool.exe")
                        .SetDescription("Octopus Tentacle Agent")
                        .SetUrl("https://octopus.com")
                        .SetFiles(files.Select(x => x.ToString())));
                }
                catch (Exception e)
                {
                    lastException = e;
                }
            });
            
            if (lastException == null) return;
        }

        if (lastException != null) throw lastException;
    }

    void SignWithAzureSignTool(AbsolutePath[] files)
    {
        var arguments = "sign " +
            $"--azure-key-vault-url \"{AzureKeyVaultUrl}\" " +
            $"--azure-key-vault-client-id \"{AzureKeyVaultAppId}\" " +
            $"--azure-key-vault-client-secret \"{AzureKeyVaultAppSecret}\" " +
            $"--azure-key-vault-certificate \"{AzureKeyVaultCertificateName}\" " +
            "--file-digest sha256 ";

        foreach (var file in files)
        {
            arguments += $"\"{file}\" ";
        }
        
        AzureSignTool(arguments);
        
        Logger.Info($"Finished signing {files.Count()} files.");
    }

    // We need to use tar directly, because .NET utilities aren't able to preserve the file permissions
    // Importantly, the Tentacle executable needs to be +x in the tar.gz file
    void TarGZipCompress(AbsolutePath inputDirectory, string fileSpec, AbsolutePath outputDirectory, string outputFile)
    {
        DockerTasks.DockerRun(settings => settings
            .EnableRm()
            .EnableTty()
            .SetVolume($"{inputDirectory}:/input", $"{outputDirectory}:/output")
            .SetCommand("debian")
            .SetArgs("tar", "-C", "/input", "-czvf", $"/output/{outputFile}", fileSpec, "--preserve-permissions"));
    }
    
    static void InBlock(string block, Action action)
    {
        if (TeamCity.Instance != null)
        {
            TeamCity.Instance.OpenBlock(block);
        }
        else
        {
            Logger.Info($"Starting {block}");
        }
        try
        {
            action();
        }
        finally
        {
            if (TeamCity.Instance != null)
            {
                TeamCity.Instance.CloseBlock(block);
            }
            else
            {
                Logger.Info($"Finished {block}");
            }
        }
    }
    #endregion
}
