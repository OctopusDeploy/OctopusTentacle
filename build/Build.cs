using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.SignTool;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.MSBuild;
using static Nuke.Common.Logger;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.SignTool.SignToolTasks;
using LogLevel = Nuke.Common.LogLevel;

class Tentacle : NukeBuild
{
    public static int Main() => Execute<Tentacle>(x => x.Default);

    //////////////////////////////////////////////////////////////////////
    // ARGUMENTS
    //////////////////////////////////////////////////////////////////////
    [Parameter] readonly string Target = "Default";
    [Parameter] readonly Verbosity Verbosity = Verbosity.Quiet;
    readonly string[] Frameworks = {"net452", "netcoreapp3.1"};
    readonly string[] RuntimeIds = {"win-x64", "linux-x64", "linux-musl-x64", "linux-arm64", "linux-arm", "osx-x64"};

    static IEnumerable<string[]> TestOnLinuxDistributions => new[]
    {
        new[] {"netcoreapp3.1", "linux-x64", "debian:buster", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "debian:oldoldstable-slim", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "debian:oldstable-slim", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "debian:stable-slim", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "linuxmintd/mint19.3-amd64", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "ubuntu:latest", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "ubuntu:rolling", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "ubuntu:trusty", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "ubuntu:xenial", "deb"},
        new[] {"netcoreapp3.1", "linux-x64", "centos:latest", "rpm"},
        new[] {"netcoreapp3.1", "linux-x64", "centos:7", "rpm"},
        new[] {"netcoreapp3.1", "linux-x64", "fedora:latest", "rpm"},
        new[] {"netcoreapp3.1", "linux-x64", "roboxes/rhel7", "rpm"},
        new[] {"netcoreapp3.1", "linux-x64", "roboxes/rhel8", "rpm"},
    };

    [Parameter]
    AbsolutePath SigningCertificatePath => RootDirectory / "certificates" / "OctopusDevelopment.pfx";

    [Parameter("signing_certificate_password")] readonly string SigningCertificatPassword = "Password01!";

    [Parameter]
    AbsolutePath GpgSigningCertificatePath => RootDirectory / "certificates" / "octopus-privkey.asc";

    [Parameter] readonly string GpgSigningCertificatePassword = "Password01!";
    [Parameter(Name = "AWS_ACCESS_KEY")] readonly string AwsAccessKeyId = "XXXX";
    [Parameter(Name = "AWS_SECRET_KEY")] readonly string AwsSecretAccessKey = "YYYY";

    [CI] TeamCity TeamCity;

    class TentacleVersionInfo
    {
        public string Major { get; set; }
        public string Minor { get; set; }
        public string Patch { get; set; }
        public string MajorMinorPatch { get; set; }
        public string PreReleaseTag { get; set; }
        public string PreReleaseTagWithDash { get; set; }
        public string BuildMetadata { get; set; }
        public string BuildMetadataWithPlus { get; set; }
        public string FullSemVer { get; set; }
    }

    readonly TentacleVersionInfo VersionInfo = DeriveVersionInfo();
    readonly string[] SigningTimestampUrls =
    {
        "http://tsa.starfieldtech.com",
        "http://www.startssl.com/timestamp",
        "http://timestamp.comodoca.com/rfc3161",
        "http://timestamp.verisign.com/scripts/timstamp.dll",
        "http://timestamp.globalsign.com/scripts/timestamp.dll"
    };
    
    AbsolutePath ArtifactsDir => RootDirectory / "_artifacts";
    AbsolutePath BuildDir => RootDirectory / "_build";
    AbsolutePath LocalPackagesDir => RootDirectory / ".." / "LocalPackages";

    AbsolutePath SourceDir => RootDirectory / "source";
    
    List<Action> Cleanups = new List<Action>();
    /*
    Setup(context =>
    {
        context.Tools.RegisterFile(RootDirectory / "signtool.exe");
    }); */
    /*
    Teardown(context =>
    {
        Info("Cleaning up");
        foreach (var cleanup in Cleanups)
            cleanup();
    }); */

    Target Clean => _ => _
        .Executes(() =>
    {
        SourceDir.GlobDirectories("**/bin").ForEach(x => EnsureCleanDirectory(x));
        SourceDir.GlobDirectories("**/obj").ForEach(x => EnsureCleanDirectory(x));
        SourceDir.GlobDirectories("**/TestResults").ForEach(x => EnsureCleanDirectory(x));
        ArtifactsDir.GlobDirectories("**").ForEach(x => EnsureCleanDirectory(x.ToString()));
        BuildDir.GlobDirectories("**").ForEach(x => EnsureCleanDirectory(x.ToString()));
    });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
    {
        DotNetRestore(_ => _
            .SetProjectFile(SourceDir / "Tentacle.sln"));
    });
    
    void ReplaceTextInFiles(AbsolutePath path, string oldValue, string newValue)
    {
        var fileText = File.ReadAllText(path);
        fileText = fileText.Replace(oldValue, newValue);
        File.WriteAllText(path, fileText);
    }
    
    void ReplaceRegexInFiles(AbsolutePath file, string oldPattern, string newPattern)
    {
        //TODO: Re-implement this
    }

    void Zip(AbsolutePath directory, AbsolutePath destination)
    {
        //TODO: re-implement this
    }

    Target VersionAssemblies => _ => _
        .Description("Modifies the VersionInfo.cs and Product.wxs files to embed version information into the shipped product.")
        .Executes(() =>
    {
        var gitBranch = DeriveGitBranch();
        var versionInfoFile = SourceDir / "Solution Items" / "VersionInfo.cs";
        RestoreFileOnCleanup(versionInfoFile);
        ReplaceRegexInFiles(versionInfoFile, "AssemblyVersion\\(\".*?\"\\)", $"AssemblyVersion(\"{VersionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyFileVersion\\(\".*?\"\\)", $"AssemblyFileVersion(\"{VersionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyInformationalVersion\\(\".*?\"\\)", $"AssemblyInformationalVersion(\"{VersionInfo.FullSemVer}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyGitBranch\\(\".*?\"\\)", $"AssemblyGitBranch(\"{gitBranch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyNuGetVersion\\(\".*?\"\\)", $"AssemblyNuGetVersion(\"{VersionInfo.FullSemVer}\")");
        
        var productWxs = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Product.wxs";
        RestoreFileOnCleanup(productWxs);
        UpdateMsiProductVersion(productWxs);
    });
    
    //TODO: Reimplement the runtime and frameworks that were dynamically built in old cake
    Target BuildWindows = _ => _
        .Description("Builds all of the win-* runtime targets.");
    
    Target BuildLinux = _ => _
        .Description("Builds all of the linux-* runtime targets.");
    
    Target BuildOsx = _ => _
        .Description("Builds all of the osx-* runtime targets.");

    Target Build => _ => _
        .Description("Build all the framework/runtime combinations. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(BuildWindows)
        .DependsOn(BuildLinux)
        .DependsOn(BuildOsx);

    Target PackWindowsZips => _ => _
        .Description("Packs the Windows .zip files containing the published binaries.")
        .DependsOn(BuildWindows)
        .Executes(() =>
    {
        EnsureExistingDirectory(ArtifactsDir/ "zip");
        var targetFrameworks = Frameworks;
        var targetRuntimeIds = RuntimeIds.Where(rid => rid.StartsWith("win-")).ToArray();
        foreach (var framework in targetFrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                var workingDir = BuildDir/ "zip"/ framework / runtimeId;
                EnsureExistingDirectory(workingDir);
                EnsureExistingDirectory(RootDirectory / workingDir/ "tentacle");
                (BuildDir / "Tentacle" / framework / runtimeId).GlobFiles("*")
                    .ForEach(x => CopyFile(x, workingDir / "tentacle"));

                //TODO: Double check these values
                Zip(workingDir, ArtifactsDir / "zip" / $"tentacle-{VersionInfo.FullSemVer}-{framework}-{runtimeId}.zip");
            }
        }
    });

    Target PackLinuxTarballs => _ => _
        .Description("Packs the Linux tarballs containing the published binaries.")
        .DependsOn(BuildLinux)
        .Executes(() =>
    {
        EnsureExistingDirectory(ArtifactsDir / "zip");
        var targetFrameworks = Frameworks.Where(f => f.StartsWith("netcore"));
        var targetRuntimeIds = RuntimeIds.Where(rid => rid.StartsWith("linux-")).ToArray();
        foreach (var framework in targetFrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                var workingDir = BuildDir / "zip" / framework / runtimeId;
                EnsureExistingDirectory(workingDir);
                EnsureExistingDirectory(workingDir / "tentacle");
                
                (RootDirectory / "linux-packages" / "content").GlobFiles("*")
                    .ForEach(x => CopyFile(x, workingDir / "tentacle"));
                (BuildDir / "Tentacle" / framework / runtimeId).GlobFiles("*")
                    .ForEach(x => CopyFile(x, workingDir / "tentacle"));
                
                //TODO: Double check these values
                TarGZipCompress(workingDir, "tentacle", ArtifactsDir / "zip", $"tentacle-{VersionInfo.FullSemVer}-{framework}-{runtimeId}.tar.gz"); 
            }
        }
    });

    Target PackOsxTarballs => _ => _
        .Description("Packs the OS/X tarballs containing the published binaries.")
        .DependsOn(BuildOsx)
        .Executes(() =>
    {
        EnsureExistingDirectory(ArtifactsDir / "zip");
        var targetTrameworks = Frameworks.Where(f => f.StartsWith("netcore"));
        var targetRuntimeIds = RuntimeIds.Where(rid => rid.StartsWith("osx-")).ToArray();
        foreach (var framework in targetTrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                var workingDir = BuildDir/ "zip" / framework / runtimeId;
                EnsureExistingDirectory(workingDir);
                EnsureExistingDirectory(workingDir / "tentacle");
                
                (RootDirectory / "linux-packages" / "content").GlobFiles("*")
                    .ForEach(x => CopyFile(x, workingDir / "tentacle"));
                (BuildDir / "Tentacle" / framework / runtimeId).GlobFiles("*")
                    .ForEach(x => CopyFile(x, workingDir / "tentacle"));
                
                //TODO: Double check these values
                TarGZipCompress(workingDir, "tentacle", ArtifactsDir / "zip", $"tentacle-{VersionInfo.FullSemVer}-{framework}-{runtimeId}.tar.gz");
            }
        }
    });
    

    //TODO: The checksum in Nuke uses MD5 whereas Cake uses SHA256. Let's convert this
    //TODO: Also check paths and stuff
    Target PackChocolateyPackage => _ => _
        .Description("Packs the Chocolatey installer.")
        .DependsOn(PackWindowsInstallers)
        .Executes(() =>
    {
        // EnsureExistingDirectory(ArtifactsDir/ "chocolatey");
        // var checksum = GetFileHash(RootDirectory / ArtifactsDir/ "msi"/ $"Octopus.Tentacle.{VersionInfo.FullSemVer}.msi");
        // var checksumValue = BitConverter.ToString(checksum.ComputedHash).Replace("-", "");
        // Info($"Checksum: Octopus.Tentacle.msi = {checksumValue}");
        // var checksum64 = GetFileHash(RootDirectory / ArtifactsDir/ "msi"/ $"Octopus.Tentacle.{VersionInfo.FullSemVer}-x64.msi");
        // var checksum64Value = BitConverter.ToString(checksum64.ComputedHash).Replace("-", "");
        // Info($"Checksum: Octopus.Tentacle-x64.msi = {checksum64Value}");
        // var chocolateyInstallScriptPath = SourceDir / "Chocolatey" / "chocolateyInstall.ps1";
        // RestoreFileOnCleanup(chocolateyInstallScriptPath);
        // ReplaceTextInFiles(chocolateyInstallScriptPath, "0.0.0", VersionInfo.FullSemVer);
        // ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum>", checksumValue);
        // ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype>", checksum.Algorithm.ToString());
        // ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum64>", checksum64Value);
        // ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype64>", checksum64.Algorithm.ToString());
        
        
        //TODO: Re-implement this
        //ChocolateyPack(SourceDir / "Chocolatey" / "OctopusDeploy.Tentacle.nuspec", new ChocolateyPackSettings{Version = VersionInfo.FullSemVer, OutputDirectory = RootDirectory / ArtifactsDir/ "chocolatey"});
    });

    Target PackLinuxPackagesLegacy => _ => _
        .Description("Legacy task until we can split creation of .rpm and .deb packages into their own tasks")
        .DependsOn(PackLinuxTarballs)
        .Executes(() =>
    {
        var targetRuntimeIds = RuntimeIds
            .Where(rid => rid.StartsWith("linux-"))
            .Where(rid => rid != "linux-musl-x64") // not supported yet. Work in progress.
            .ToArray();
        
        foreach (var runtimeId in targetRuntimeIds)
        {
            CreateLinuxPackages(runtimeId);
            EnsureExistingDirectory(ArtifactsDir / "deb");
            EnsureExistingDirectory(ArtifactsDir / "rpm");
            
            (BuildDir / "deb" / runtimeId / "output").GlobFiles("*.deb")
                .ForEach(x => CopyFile(x, ArtifactsDir / "deb"));
            (BuildDir / "deb" / runtimeId / "output").GlobFiles("*.rpm")
                .ForEach(x => CopyFile(x, ArtifactsDir / "rpm"));
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
        EnsureExistingDirectory(ArtifactsDir/ "msi");
        
        var installerDir = BuildDir/ "Installer";
        EnsureExistingDirectory(installerDir);
        
        (BuildDir / "Tentacle" / "net452" / "win-x64").GlobFiles("*")
            .ForEach(x => CopyFile(x, installerDir));
        (BuildDir / "Octopus.Manager.Tentacle" / "net452" / "win-x64").GlobFiles("*")
            .ForEach(x => CopyFile(x, installerDir));
        (RootDirectory / "scripts").GlobFiles("Harden-InstallationDirectory.ps1")
            .ForEach(x => CopyFile(x, installerDir));
        
        GenerateMsiInstallerContents(installerDir);
        BuildMsiInstallerForPlatform(MSBuildTargetPlatform.x64);
        BuildMsiInstallerForPlatform(MSBuildTargetPlatform.x86);
    });

    Target PackCrossPlatformBundle => _ => _
        .Description("Packs the cross-platform Tentacle.nupkg used by Octopus Server to dynamically upgrade Tentacles.")
        .DependsOn(BuildWindows)
        .DependsOn(PackWindowsInstallers)
        .DependsOn(PackDebianPackage)
        .DependsOn(PackRedHatPackage)
        .DependsOn(PackWindowsZips)
        .DependsOn(PackLinuxTarballs)
        .DependsOn(PackOsxTarballs)
        .Executes(() =>
    {
        EnsureExistingDirectory(ArtifactsDir / "nuget");
        
        var workingDir = BuildDir / "Octopus.Tentacle.CrossPlatformBundle";
        EnsureExistingDirectory(workingDir);
        
        var debAMD64PackageFilename = ConstructDebianPackageFilename("tentacle", VersionInfo, "amd64");
        var debARM64PackageFilename = ConstructDebianPackageFilename("tentacle", VersionInfo, "arm64");
        var debARM32PackageFilename = ConstructDebianPackageFilename("tentacle", VersionInfo, "armhf");
        
        var rpmARM64PackageFilename = ConstructRedHatPackageFilename("tentacle", VersionInfo, "aarch64");
        var rpmARM32PackageFilename = ConstructRedHatPackageFilename("tentacle", VersionInfo, "armv7hl");
        var rpmx64PackageFilename = ConstructRedHatPackageFilename("tentacle", VersionInfo, "x86_64");
        
        CopyFile(SourceDir / "Octopus.Tentacle.CrossPlatformBundle" / "Octopus.Tentacle.CrossPlatformBundle.nuspec", workingDir);
        CopyFile(ArtifactsDir/ "msi" / $"Octopus.Tentacle.{VersionInfo.FullSemVer}.msi", workingDir/ "Octopus.Tentacle.msi");
        CopyFile(ArtifactsDir/ "msi" / $"Octopus.Tentacle.{VersionInfo.FullSemVer}-x64.msi", workingDir/ "Octopus.Tentacle-x64.msi");
        (BuildDir / "Octopus.Tentacle.Upgrader" / "net452" / "win-x64").GlobFiles("*")
            .ForEach(x => CopyFile(x, workingDir));
        CopyFile(ArtifactsDir / "deb" / debAMD64PackageFilename, workingDir / debAMD64PackageFilename);
        CopyFile(ArtifactsDir / "deb" / debARM64PackageFilename, workingDir / debARM64PackageFilename);
        CopyFile(ArtifactsDir / "deb" / debARM32PackageFilename, workingDir / debARM32PackageFilename);
        CopyFile(ArtifactsDir / "rpm" / rpmARM64PackageFilename, workingDir / rpmARM64PackageFilename);
        CopyFile(ArtifactsDir / "rpm" / rpmARM32PackageFilename, workingDir / rpmARM32PackageFilename);
        CopyFile(ArtifactsDir / "rpm" / rpmx64PackageFilename, workingDir / rpmx64PackageFilename);
        
        foreach (var framework in Frameworks)
        {
            foreach (var runtimeId in RuntimeIds)
            {
                if (framework == "net452" && runtimeId != "win-x64")
                    continue; // General exclusion of net452+(not Windows)
                var fileExtension = runtimeId.StartsWith("win-") ? "zip" : "tar.gz";
                CopyFile(ArtifactsDir / "zip" / $"tentacle-{VersionInfo.FullSemVer}-{framework}-{runtimeId}.{fileExtension}", workingDir / $"tentacle-{framework}-{runtimeId}.{fileExtension}");
            }
        }

        // Assert that some key files exist.
        AssertFileExists(workingDir/ "Octopus.Tentacle.msi");
        AssertFileExists(workingDir/ "Octopus.Tentacle-x64.msi");
        AssertFileExists(workingDir/ "Octopus.Tentacle.Upgrader.exe");
        AssertFileExists(workingDir/ debAMD64PackageFilename);
        AssertFileExists(workingDir/ debARM64PackageFilename);
        AssertFileExists(workingDir/ debARM32PackageFilename);
        AssertFileExists(workingDir/ rpmARM64PackageFilename);
        AssertFileExists(workingDir/ rpmARM32PackageFilename);
        AssertFileExists(workingDir/ rpmx64PackageFilename);
        RunProcess("dotnet", $"tool run dotnet-octo pack --id=Octopus.Tentacle.CrossPlatformBundle --version={VersionInfo.FullSemVer} --basePath={workingDir} --outFolder={ArtifactsDir / "nuget"}");
    });

    Target PackWindows => _ => _
        .Description("Packs all the Windows targets.")
        .DependsOn(PackWindowsZips)
        .DependsOn(PackChocolateyPackage)
        .DependsOn(PackWindowsInstallers);

    Target PackLinux => _ => _
        .Description("Packs all the Linux targets.")
        .DependsOn(PackLinuxTarballs)
        .DependsOn(PackDebianPackage)
        .DependsOn(PackRedHatPackage);

    Target PackOSX => _ => _
        .Description(RootDirectory / "Packs all the OS" / "X targets.")
        .DependsOn(PackOsxTarballs);

    Target Pack => _ => _
        .Description("Pack all the artifacts. Notional task - running this on a single host is possible but cumbersome.")
        .DependsOn(PackCrossPlatformBundle)
        .DependsOn(PackWindows)
        .DependsOn(PackLinux)
        .DependsOn(PackOSX);
    
    Target TestLinuxPackagesTask = _ => _
        .Description("Tests installing the .deb and .rpm packages onto all of the Linux target distributions.")
        .Executes(() =>
    {
        InTestSuite("Test-LinuxPackages", () =>
        {
            foreach (var testConfiguration in TestOnLinuxDistributions)
            {
                var framework = testConfiguration[0];
                var runtimeId = testConfiguration[1];
                var dockerImage = testConfiguration[2];
                var packageType = testConfiguration[3];
                RunLinuxPackageTestsFor(framework, runtimeId, dockerImage, packageType);
            }
        });
    });

    Target CopyToLocalPackages => _ => _
        .Description("If not running on a build agent, this step copies the relevant built artifacts to the local packages cache.")
        .OnlyWhenStatic(() => IsLocalBuild)
        .DependsOn(PackCrossPlatformBundle)
        .Executes(() =>
    {
        EnsureExistingDirectory(LocalPackagesDir);
        CopyFileToDirectory(ArtifactsDir / $"Tentacle.{VersionInfo.FullSemVer}.nupkg", LocalPackagesDir);
    });

    Target Default => _ => _
        .DependsOn(Pack)
        .DependsOn(CopyToLocalPackages);

    //////////////////////////////////////////////////////////////////////
    // IMPLEMENTATION DETAILS
    //////////////////////////////////////////////////////////////////////
    string DeriveGitBranch()
    {
        var branch = Environment.GetEnvironmentVariable("OCTOVERSION_CurrentBranch");
        if (string.IsNullOrEmpty(branch))
        {
            Warn("Git branch not available from environment variable. Attempting to work it out for ourselves. DANGER: Don't rely on this on your build server!");
            if (IsRunningOnTeamCity)
            {
                var message = "Git branch not available from environment variable";
                Console.WriteLine($"##teamcity[message text='{message}' status='FAILURE']");
                throw new NotSupportedException(message);
            }

            branch = "refs/heads/" + RunProcessAndGetOutput("git", "rev-parse --abbrev-ref HEAD");
        }

        return branch;
    }
    
    static bool IsRunningOnTeamCity => TeamCity.Instance != null;

    static TentacleVersionInfo DeriveVersionInfo()
    {
        var existingFullSemVer = Environment.GetEnvironmentVariable("OCTOVERSION_FullSemVer");
        if (string.IsNullOrEmpty(existingFullSemVer))
        {
            var branch = DeriveGitBranch();
            Environment.SetEnvironmentVariable("OCTOVERSION_CurrentBranch", branch);
        }

        var octoVersionArgs = IsRunningOnTeamCity ? "--OutputFormats:0=Console --OutputFormats:1=TeamCity" : "--OutputFormats:0=Console";
        RunProcess("dotnet", $"tool run octoversion {octoVersionArgs}");
        var versionJson = RunProcessAndGetOutput("dotnet", "tool run octoversion --OutputFormats:0=Json");
        var version = Newtonsoft.Json.JsonConvert.DeserializeObject<TentacleVersionInfo>(versionJson);
        Info("Building OctopusTentacle {0}", version.FullSemVer);
        return version;
    }

    void InBlock(string block, Action action)
    {
        if (IsRunningOnTeamCity)
            TeamCity.OpenBlock(block);
        else
            Info($"Starting {block}");
        try
        {
            action();
        }
        finally
        {
            if (IsRunningOnTeamCity)
                TeamCity.CloseBlock(block);
            else
                Info($"Finished {block}");
        }
    }

    static void InTestSuite(string testSuite, Action action)
    {
        if (IsRunningOnTeamCity)
            Console.WriteLine($"##teamcity[testSuiteStarted name='{testSuite}']");
        try
        {
            action();
        }
        finally
        {
            if (IsRunningOnTeamCity)
                Console.WriteLine($"##teamcity[testSuiteFinished name='{testSuite}']");
        }
    }

    static void InTest(string test, Action action)
    {
        var startTime = DateTimeOffset.UtcNow;
        try
        {
            if (IsRunningOnTeamCity)
                Console.WriteLine($"##teamcity[testStarted name='{test}' captureStandardOutput='true']");
            action();
        }
        catch (Exception ex)
        {
            if (IsRunningOnTeamCity)
                Console.WriteLine($"##teamcity[testFailed name='{test}' message='{ex.Message}']");
            Error(ex.ToString());
        }
        finally
        {
            var finishTime = DateTimeOffset.UtcNow;
            var elapsed = finishTime - startTime;
            if (IsRunningOnTeamCity)
                Console.WriteLine($"##teamcity[testFinished name='{test}' duration='{elapsed.TotalMilliseconds}']");
        }
    }

    void RestoreFileOnCleanup(string file)
    {
        var contents = System.IO.File.ReadAllBytes(file);
        Cleanups.Add(() =>
        {
            Info("Restoring {0}", file);
            System.IO.File.WriteAllBytes(file, contents);
        });
    }

    void UpdateMsiProductVersion(string productWxs)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(productWxs);
        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");
        var product = xmlDoc.SelectSingleNode("//wi:Product", nsmgr);
        product.Attributes["Version"].Value = VersionInfo.MajorMinorPatch;
        xmlDoc.Save(productWxs);
    }

    void GenerateMsiInstallerContents(string installerDir)
    {
        InBlock("Running HEAT to generate the installer contents...", () =>
        {
            var harvestDirectory = installerDir;
            var harvestFile = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Tentacle.Generated.wxs";
            RestoreFileOnCleanup(harvestFile);
            var heatSettings = new HeatSettings{NoLogo = true, GenerateGuid = true, SuppressFragments = true, SuppressRootDirectory = true, SuppressRegistry = true, SuppressUniqueIds = true, ComponentGroupName = "TentacleComponents", PreprocessorVariable = "var.TentacleSource", DirectoryReferenceId = "INSTALLLOCATION"};
            WiXHeat(harvestDirectory, harvestFile, WiXHarvestType.Dir, heatSettings);
        });
    }

    void BuildMsiInstallerForPlatform(MSBuildTargetPlatform platformTarget)
    {
        InBlock($"Building {platformTarget} installer", () =>
        {
            MSBuild(_ => _
            .SetTargetPath(RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "Octopus.Tentacle.Installer.wixproj"));
            var builtMsi = RootDirectory / "installer" / "Octopus.Tentacle.Installer" / "bin" / platformTarget.ToString() / "Octopus.Tentacle.msi";
            SignAndTimeStamp(builtMsi);
            var platformStr = Equals(platformTarget, MSBuildTargetPlatform.x64) ? "-x64" : "";
            var artifactDestination = RootDirectory / ArtifactsDir/ "msi"/ $"Octopus.Tentacle.{VersionInfo.FullSemVer}{platformStr}.msi";
            MoveFile(builtMsi, artifactDestination);
        });
    }

    void CreateLinuxPackages(string runtimeId)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGN_PRIVATE_KEY")))
        {
            throw new Exception("This build requires environment variables `SIGN_PRIVATE_KEY` (in a format gpg1 can import)" + " and `SIGN_PASSPHRASE`, which are used to sign the .rpm.");
        }

        //TODO It's probable that the .deb and .rpm package layouts will be different - and potentially _should already_ be different.
        // We're approaching this with the assumption that we'll split .deb and .rpm creation soon, which means that we'll create a separate
        // filesystem layout for each of them. Using .deb for now; expecting to replicate that soon for .rpm.
        var debBuildDir = RootDirectory / BuildDir/ "deb" / runtimeId;
        EnsureExistingDirectory(debBuildDir);
        EnsureExistingDirectory(debBuildDir / "scripts");
        EnsureExistingDirectory(debBuildDir / "output");
        
        // Use fully-qualified paths here so that the bind mount points work correctly.
        (RootDirectory / "linux-packages/packaging-scripts").GlobFiles("*")
            .ForEach(x => CopyFile(x, debBuildDir / "scripts"));
        var scriptsBindMountPoint = new DirectoryInfo(debBuildDir/ "scripts").FullName;
        var inputBindMountPoint = new DirectoryInfo(BuildDir/ "zip"/ "netcoreapp3.1"/ runtimeId/ "tentacle").FullName;
        var outputBindMountPoint = new DirectoryInfo(debBuildDir/ "output").FullName;

        DockerTasks.DockerPull("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest");
        DockerPull("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest");
        DockerRunWithoutResult(new DockerContainerRunSettings{Rm = true, Tty = true, Env = new string[]{$"VERSION={VersionInfo.FullSemVer}", RootDirectory / "INPUT_PATH=" / "input", RootDirectory / "OUTPUT_PATH=" / "output", "SIGN_PRIVATE_KEY", "SIGN_PASSPHRASE"}, Volume = new string[]{RootDirectory / $"{scriptsBindMountPoint}:" / "scripts", RootDirectory / $"{inputBindMountPoint}:" / "input", RootDirectory / $"{outputBindMountPoint}:" / "output"}}, "docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest", RootDirectory / "bash "/ "scripts"/ $"package.sh {runtimeId}");
    }

    static void RunLinuxPackageTestsFor(string framework, string runtimeId, string dockerImage, string packageType)
    {
        InTest(RootDirectory / framework/ runtimeId/ dockerImage/ packageType, () =>
        {
            string archSuffix = null;
            if (packageType == "deb")
            {
                if (runtimeId == "linux-x64")
                    archSuffix = "_amd64";
            }
            else if (packageType == "rpm")
            {
                if (runtimeId == "linux-x64")
                    archSuffix = ".x86_64";
            }

            if (string.IsNullOrEmpty(archSuffix))
                throw new NotSupportedException();
            var packageFileSpec = (RootDirectory / $"_artifacts/{packageType}").GlobFiles("*{archSuffix}.{packageType}");
            Info($"Searching for files in {packageFileSpec}");
            var packageFile = new System.IO.FileInfo(packageFileSpec.AsEnumerable().Single());
            Info($"Testing Linux package file {packageFile.Name}");
            var testScriptsBindMountPoint = new System.IO.DirectoryInfo(RootDirectory / "linux-packages"/ "test-scripts").FullName;
            var artifactsBindMountPoint = new System.IO.DirectoryInfo("_artifacts").FullName;
            DockerPull(dockerImage);
            DockerRunWithoutResult(new DockerContainerRunSettings{Rm = true, Tty = true, Env = new string[]{$"VERSION={VersionInfo.FullSemVer}", RootDirectory / "INPUT_PATH=" / "input", RootDirectory / "OUTPUT_PATH=" / "output", "SIGN_PRIVATE_KEY", "SIGN_PASSPHRASE", "REDHAT_SUBSCRIPTION_USERNAME", "REDHAT_SUBSCRIPTION_PASSWORD", $"BUILD_NUMBER={VersionInfo.FullSemVer}"}, Volume = new string[]{RootDirectory / $"{testScriptsBindMountPoint}:" / "test-scripts:ro", RootDirectory / $"{artifactsBindMountPoint}:" / "artifacts:ro"}}, dockerImage, RootDirectory / "bash "/ "test-scripts"/ "test-linux-package.sh "/ "artifacts"/ packageType/ packageFile.Name);
        });
    }

    // We need to use tar directly, because .NET utilities aren't able to preserve the file permissions
    // Importantly, the Tentacle executable needs to be +x in the tar.gz file
    void TarGZipCompress(AbsolutePath inputDirectory, string filespec, AbsolutePath outputDirectory, string outputFile)
    {
        var inputMountPoint = new System.IO.DirectoryInfo(inputDirectory).FullName;
        var outputMountPoint = new System.IO.DirectoryInfo(outputDirectory).FullName;
        DockerRunWithoutResult(new DockerContainerRunSettings{Rm = true, Tty = true, Volume = new string[]{RootDirectory / $"{inputMountPoint}:" / "input", RootDirectory / $"{outputMountPoint}:" / "output", }}, "debian", RootDirectory / "tar -C "/ "input -czvf "/ "output"/ $"{outputFile} {filespec} --preserve-permissions");
    }

    // note: Doesn't check if existing signatures are valid, only that one exists
    // source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
    bool HasAuthenticodeSignature(AbsolutePath fileInfo)
    {
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

    void SignAndTimeStamp(params AbsolutePath[] filePaths)
    {
        InBlock("Signing and timestamping...", () =>
        {
            foreach (var filePath in filePaths)
            {
                if (!FileExists(filePath))
                    throw new Exception($"File {filePath} does not exist");
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.IsReadOnly)
                {
                    InBlock($"{filePath} is readonly. Making it writeable.", () =>
                    {
                        fileInfo.IsReadOnly = false;
                    });
                }
            }

            var lastException = default(Exception);

            foreach (var timestampServerUrl in SigningTimestampUrls)
            {
                InBlock($"Trying to time stamp [{string.Join(Environment.NewLine, filePaths.Select(a => a.ToString()))}] using {timestampServerUrl}", () =>
                {
                    try
                    {
                        SignTool(_ => _
                            .SetFile(SigningCertificatePath)
                            .SetPassword(SigningCertificatPassword)
                            .SetFileDigestAlgorithm("sha256")
                            .SetDescription("Octopus Tentacle Agent")
                            .SetUrl("http://octopus.com")
                            .SetRfc3161TimestampServerUrl(timestampServerUrl)
                            .SetFiles(filePaths.Select(x => x.ToString())));
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
                });
                
                if (lastException == null)
                    return;
            }

            throw lastException;
        });
    }

    static string RunProcessAndGetOutput(string fileName, string args)
    {
        var stdoutStringBuilder = new StringBuilder();
        
        var exitCode = Octopus.SilentProcessRunner.SilentProcessRunner.ExecuteCommand(
            executable: fileName,
            arguments: args,
            workingDirectory: Environment.CurrentDirectory,
            debug: log => {},
            info: log => stdoutStringBuilder.Append(log),
            error: Error);
        
        var stdout = stdoutStringBuilder.ToString();
        if (exitCode != 0)
        {
            //TODO: Can we still rendersafe?
            // var safeArgs = settings.Arguments.RenderSafe(); 
            //throw new Exception($"{fileName} {safeArgs} failed with {exitCode} exitcode.");
            throw new Exception($"{fileName} failed with {exitCode} exitcode.");
        }

        return stdout;
    }

    static void RunProcess(string fileName, string args)
    {
        var exitCode = Octopus.SilentProcessRunner.SilentProcessRunner.ExecuteCommand(
            executable: fileName,
            arguments: args,
            workingDirectory: Environment.CurrentDirectory,
            debug: log => {},
            info: Info,
            error: Error);
        
        if (exitCode != 0)
        {
            //TODO: Can we still rendersafe?
            // var safeArgs = settings.Arguments.RenderSafe(); 
            //throw new Exception($"{fileName} {safeArgs} failed with {exitCode} exitcode.");
            throw new Exception($"{fileName} failed with {exitCode} exitcode.");
        }
    }

    void RunBuildFor(string framework, string runtimeId)
    {
        // 1. Build everything
        // 2. If Windows, sign the binaries
        // 3. Publish everything. This avoids signing binaries twice.
        var configuration = $"Release-{framework}-{runtimeId}";
        DotNetBuild(_ => _
            .SetProjectFile(SourceDir / "Tentacle.sln")
            .SetConfiguration(configuration)
            .SetFramework(framework)
            //.SetMSBuildSettings(new DotNetCoreMSBuildSettings{MaxCpuCount = 256}) //TODO: How to set this?
            .SetNoRestore(true)
            .SetRuntime(runtimeId));
        DotNetPublish(_ => _
            .SetProject(SourceDir / "Tentacle.sln")
            .SetProcessArgumentConfigurator(args => args.Add($"/p:Version={VersionInfo.FullSemVer}"))
            .SetConfiguration(configuration)
            .SetFramework(framework)
            .SetNoBuild(true)
            .SetNoRestore(true)
            .SetRuntime(runtimeId));
        if (runtimeId.StartsWith("win-"))
        {
            // Sign any unsigned libraries that Octopus Deploy authors so that they play nicely with security scanning tools.
            // Refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
            // Decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
            var windowsOnlyBuiltFileSpec = BuildDir.GlobDirectories("**/{framework}/{runtimeId}/**");

            var filesToSign = windowsOnlyBuiltFileSpec
                .SelectMany(x => x.GlobFiles("**/Octo*.exe", "**/Octo*.dll", "**/Tentacle.exe", "**/Tentacle.dll", "**/Nuget.*.dll"))
                .Where(file => !HasAuthenticodeSignature(file));
            
            SignAndTimeStamp(filesToSign.ToArray());
        }
    }

    void RunTestSuiteFor(string framework, string runtimeId)
    {
        EnsureExistingDirectory(RootDirectory / ArtifactsDir/ "teamcity");
        // We call dotnet test against the assemblies directly here because calling it against the .sln requires
        // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
        // have to shunt them all around the place.
        // By doing things this way, we can have a seamless experience between local and remote builds.
        var testAssembliesPath = (BuildDir / "Octopus.Tentacle.Tests" / framework / runtimeId)
            .GlobFiles("*.Tests.dll");
        var testResultsPath = new FileInfo(ArtifactsDir / "teamcity" / $"TestResults-{framework}-{runtimeId}.xml").FullName;
        try
        {
            // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
            // when the test runner is being asked to build things, not when they're already built.
            // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
            DotNetTest(_ => _
                .SetProjectFile(testAssembliesPath.FirstOrDefault())
                .SetFramework(framework)
                .SetProcessArgumentConfigurator(args => args.Add($"--logger \"trx;LogFileName={testResultsPath}\"")));
        }
        catch (Exception ex)
        {
            Warn($"{ex.Message}: {ex}");
            // We want Cake to continue running even if tests fail. It's the responsibility of the build system to inspect
            // the test results files and assert on whether a failed test should fail the build. E.g. muted, ignored tests.
        }
    }

    string ConstructDebianPackageFilename(string packageName, TentacleVersionInfo versionInfo, string architecture)
    {
        var filename = $"{packageName}_{VersionInfo.FullSemVer}_{architecture}.deb";
        return filename;
    }

    string ConstructRedHatPackageFilename(string packageName, TentacleVersionInfo versionInfo, string architecture)
    {
        var transformedVersion = VersionInfo.FullSemVer.Replace("-", "_");
        var filename = $"{packageName}-{transformedVersion}-1.{architecture}.rpm";
        return filename;
    }

    void AssertFileExists(string fileSpec)
    {
        var files = fileSpec;
        foreach (var file in files)
            Info($"Found file: {file}");
        if (!files.Any())
            throw new Exception($"No file(s) matching {fileSpec} were found.");
    }
}