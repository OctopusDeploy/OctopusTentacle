//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#module nuget:?package=Cake.DotNetTool.Module&version=0.4.0

// NOTE: This solution uses dotnet core tooling in preference to Cake tools where possible.
// dotnet tools are installed via the `dotnet tool install` command (not via this Cakefile),
// and are restored using `dotnet tool restore` on build.

#tool "dotnet:?package=AzureSignTool&version=2.0.17"
#tool "nuget:?package=TeamCity.Dotnet.Integration&version=1.0.10"
#tool "nuget:?package=WiX&version=3.11.2"
#addin "nuget:?package=Cake.Docker&version=0.10.0"
#addin "nuget:?package=Cake.FileHelpers&version=3.2.1"
#addin "nuget:?package=Cake.Incubator&version=5.0.1"
#addin "nuget:?package=Cake.Json&version=5.2"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=SharpZipLib&version=1.2.0"
#tool "nuget:?package=Chocolatey&version=0.10.8"

using Path = System.IO.Path;
using Dir = System.IO.Directory;
using System.Xml;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;


//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var verbosity = Argument<Verbosity>("verbosity", Verbosity.Quiet);
var frameworks = new [] { "netcoreapp3.1", "net452"};
var runtimeIds = new [] { "win", "win-x86", "win-x64", "linux-x64", "linux-musl-x64", "linux-arm64", "linux-arm", "osx-x64" };
var testOnLinuxDistributions = new string[][] {
    new [] { "netcoreapp3.1", "linux-x64", "debian:buster", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "debian:oldoldstable-slim", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "debian:oldstable-slim", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "debian:stable-slim", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "linuxmintd/mint19.3-amd64", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "ubuntu:latest", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "ubuntu:rolling", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "ubuntu:trusty", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "ubuntu:xenial", "deb" },
    new [] { "netcoreapp3.1", "linux-x64", "centos:latest", "rpm" },
    new [] { "netcoreapp3.1", "linux-x64", "centos:7", "rpm" },
    new [] { "netcoreapp3.1", "linux-x64", "fedora:latest", "rpm" },
    new [] { "netcoreapp3.1", "linux-x64", "roboxes/rhel7", "rpm" },
    new [] { "netcoreapp3.1", "linux-x64", "roboxes/rhel8", "rpm" },
};

var keyVaultUrl = Argument("AzureKeyVaultUrl", "");
var keyVaultAppId = Argument("AzureKeyVaultAppId", "");
var keyVaultAppSecret = Argument("AzureKeyVaultAppSecret", "");
var keyVaultCertificateName = Argument("AzureKeyVaultCertificateName", "");

var signingCertificatePath = Argument("signing_certificate_path", "./certificates/OctopusDevelopment.pfx");
var signingCertificatePassword = Argument("signing_certificate_password", "Password01!");

var awsAccessKeyId = Argument("aws_access_key_id", EnvironmentVariable("AWS_ACCESS_KEY") ?? "XXXX");
var awsSecretAccessKey = Argument("aws_secret_access_key", EnvironmentVariable("AWS_SECRET_KEY") ?? "YYYY");

public class VersionInfo
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
    public string NuGetVersion { get; set; }
}

var versionInfo = DeriveVersionInfo();

// Keep this list in order by most likely to succeed
var signingTimestampUrls = new string[] {
    "http://tsa.starfieldtech.com",
    "http://www.startssl.com/timestamp",
    "http://timestamp.comodoca.com/rfc3161",
    "http://timestamp.verisign.com/scripts/timstamp.dll",
    "http://timestamp.globalsign.com/scripts/timestamp.dll"
    };


var artifactsDir = "./_artifacts";
var buildDir = "./_build";
var localPackagesDir = "../LocalPackages";


//////////////////////////////////////////////////////////////////////
// SETUP/TEARDOWN
//////////////////////////////////////////////////////////////////////

var cleanups = new List<Action>();

Setup(context =>
{
    context.Tools.RegisterFile("./signtool.exe");
});

Teardown(context =>
{
    Information("Cleaning up");
    foreach(var cleanup in cleanups)
        cleanup();
});


//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        CleanDirectories("./source/**/bin");
        CleanDirectories("./source/**/obj");
        CleanDirectories("./source/**/TestResults");
        CleanDirectories(artifactsDir);
        CleanDirectories(buildDir);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetCoreRestore("./source/Tentacle.sln");
    });

Task("VersionAssemblies")
    .Description("Modifies the VersionInfo.cs and Product.wxs files to embed version information into the shipped product.")
    .Does(() =>
    {
        var gitBranch = DeriveGitBranch();

        var versionInfoFile = "./source/Solution Items/VersionInfo.cs";
        RestoreFileOnCleanup(versionInfoFile);
        ReplaceRegexInFiles(versionInfoFile, "AssemblyVersion\\(\".*?\"\\)", $"AssemblyVersion(\"{versionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyFileVersion\\(\".*?\"\\)", $"AssemblyFileVersion(\"{versionInfo.MajorMinorPatch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyInformationalVersion\\(\".*?\"\\)", $"AssemblyInformationalVersion(\"{versionInfo.FullSemVer}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyGitBranch\\(\".*?\"\\)", $"AssemblyGitBranch(\"{gitBranch}\")");
        ReplaceRegexInFiles(versionInfoFile, "AssemblyNuGetVersion\\(\".*?\"\\)", $"AssemblyNuGetVersion(\"{versionInfo.FullSemVer}\")");

        var productWxs = "./installer/Octopus.Tentacle.Installer/Product.wxs";
        RestoreFileOnCleanup(productWxs);
        UpdateMsiProductVersion(productWxs);
    });

// This task will have dependencies programmatically added to it below.
var taskBuildWindows = Task("Build-Windows")
    .Description("Builds all of the win-* runtime targets.");

// This task will have dependencies programmatically added to it below.
var taskBuildLinux = Task("Build-Linux")
    .Description("Builds all of the linux-* runtime targets.");

// This task will have dependencies programmatically added to it below.
var taskBuildOSX = Task("Build-OSX")
    .Description("Builds all of the osx-* runtime targets.");

// This block defines tasks looking like this:
//
// Task("Build-<framework>-<runtimeId>")
//
// We dynamically define build tasks based on the cross-product of frameworks and runtimes.
// We do this rather than attempting to have a single "Build" task because, although we can
// run the actual compilation task for all targets on a single OS, we can't run the associated
// packaging tasks. E.g. we need Windows (or wine) to run WiX, but we also need a Linux Docker
// machine to package .deb and .rpm files. This makes it more sensible to split the compilation
// for different platforms into separate tasks, and then have a single unifying task only for
// local completeness.
foreach (var framework in frameworks)
{
    foreach (var runtimeId in runtimeIds )
    {
        if (runtimeId == "win" && framework != "net452"
         || runtimeId != "win" && framework == "net452") continue;

        var taskName = $"Build-{framework}-{runtimeId}";
        Task(taskName)
            .IsDependentOn("Restore")
            .IsDependentOn("VersionAssemblies")
            .Description($"Builds and publishes for {framework}/{runtimeId}.")
            .Does(() => {
                RunBuildFor(framework, runtimeId);
            });

        // Include this task in the dependencies of whichever is the appropriate rolled-up build task for its operating system.
        if (runtimeId.StartsWith("win"))
        {
            taskBuildWindows.IsDependentOn(taskName);
        }
        else if (runtimeId.StartsWith("linux-"))
        {
            taskBuildLinux.IsDependentOn(taskName);
        }
        else if (runtimeId.StartsWith("osx-"))
        {
            taskBuildOSX.IsDependentOn(taskName);
        }
    }
}

Task("Build")
    .Description("Build all the framework/runtime combinations. Notional task - running this on a single host is possible but cumbersome.")
    .IsDependentOn("Build-Windows")
    .IsDependentOn("Build-Linux")
    .IsDependentOn("Build-OSX")
    ;

Task("Pack-WindowsZips")
    .Description("Packs the Windows .zip files containing the published binaries.")
    .IsDependentOn("Build-Windows")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/zip");

        var targetFrameworks = frameworks;
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("win")).ToArray();

        foreach (var framework in targetFrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                if (runtimeId == "win" && framework != "net452"
                 || runtimeId != "win" && framework == "net452") continue; //Pack net452 only for the AnyCPU runtime (ie 'win'), and don't pack anything else for it.

                Information($"Packing: {framework}, {runtimeId}");
                var workingDir = $"{buildDir}/zip/{framework}/{runtimeId}";
                CreateDirectory(workingDir);
                CreateDirectory($"{workingDir}/tentacle");
                CopyFiles($"{buildDir}/Tentacle/{framework}/{runtimeId}/*", $"{workingDir}/tentacle/");
                Zip($"{workingDir}", $"{artifactsDir}/zip/tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.zip");
            }
        }
    });

Task("Pack-LinuxTarballs")
    .Description("Packs the Linux tarballs containing the published binaries.")
    .IsDependentOn("Build-Linux")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/zip");

        var targetFrameworks = frameworks.Where(f => f.StartsWith("netcore"));
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("linux-")).ToArray();

        foreach (var framework in targetFrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                var workingDir = $"{buildDir}/zip/{framework}/{runtimeId}";
                CreateDirectory(workingDir);
                CreateDirectory($"{workingDir}/tentacle");
                CopyFiles($"./linux-packages/content/*", $"{workingDir}/tentacle/");
                CopyFiles($"{buildDir}/Tentacle/{framework}/{runtimeId}/*", $"{workingDir}/tentacle/");
                TarGZipCompress(workingDir, "tentacle", $"{artifactsDir}/zip", $"tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.tar.gz");
            }
        }
    });

Task("Pack-OSXTarballs")
    .Description("Packs the OS/X tarballs containing the published binaries.")
    .IsDependentOn("Build-OSX")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/zip");

        var targetFrameworks = frameworks.Where(f => f.StartsWith("netcore"));
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("osx-")).ToArray();

        foreach (var framework in targetFrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                var workingDir = $"{buildDir}/zip/{framework}/{runtimeId}";
                CreateDirectory(workingDir);
                CreateDirectory($"{workingDir}/tentacle");
                CopyFiles($"./linux-packages/content/*", $"{workingDir}/tentacle/");
                CopyFiles($"{buildDir}/Tentacle/{framework}/{runtimeId}/*", $"{workingDir}/tentacle/");
                TarGZipCompress(workingDir, "tentacle", $"{artifactsDir}/zip", $"tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.tar.gz");
            }
        }
    });

Task("Pack-ChocolateyPackage")
    .Description("Packs the Chocolatey installer.")
    .IsDependentOn("Pack-WindowsInstallers")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/chocolatey");

        var checksum = CalculateFileHash(File($"{artifactsDir}/msi/Octopus.Tentacle.{versionInfo.FullSemVer}.msi"));
        var checksumValue = BitConverter.ToString(checksum.ComputedHash).Replace("-", "");
        Information($"Checksum: Octopus.Tentacle.msi = {checksumValue}");

        var checksum64 = CalculateFileHash(File($"{artifactsDir}/msi/Octopus.Tentacle.{versionInfo.FullSemVer}-x64.msi"));
        var checksum64Value = BitConverter.ToString(checksum64.ComputedHash).Replace("-", "");
        Information($"Checksum: Octopus.Tentacle-x64.msi = {checksum64Value}");

        var chocolateyInstallScriptPath = "./source/Chocolatey/chocolateyInstall.ps1";
        RestoreFileOnCleanup(chocolateyInstallScriptPath);

        ReplaceTextInFiles(chocolateyInstallScriptPath, "0.0.0", versionInfo.FullSemVer);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum>", checksumValue);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype>", checksum.Algorithm.ToString());
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksum64>", checksum64Value);
        ReplaceTextInFiles(chocolateyInstallScriptPath, "<checksumtype64>", checksum64.Algorithm.ToString());

        ChocolateyPack("./source/Chocolatey/OctopusDeploy.Tentacle.nuspec", new ChocolateyPackSettings
        {
            Version = versionInfo.NuGetVersion,
            OutputDirectory = $"{artifactsDir}/chocolatey"
        });
    });


Task("Pack-LinuxPackages-Legacy")
    .IsDependentOn("Pack-LinuxTarballs")
    .Description("Legacy task until we can split creation of .rpm and .deb packages into their own tasks")
    .Does(() => {
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("linux-"))
        .Where(rid => rid != "linux-musl-x64")  // not supported yet. Work in progress.
        .ToArray();

        foreach (var runtimeId in targetRuntimeIds)
        {
            CreateLinuxPackages(runtimeId);

            CreateDirectory($"{artifactsDir}/deb");
            CopyFiles($"{buildDir}/deb/{runtimeId}/output/*.deb", $"{artifactsDir}/deb");

            CreateDirectory($"{artifactsDir}/rpm");
            CopyFiles($"{buildDir}/deb/{runtimeId}/output/*.rpm", $"{artifactsDir}/rpm");
        }
    });

Task("Pack-DebianPackage")
    .IsDependentOn("Pack-LinuxPackages-Legacy")
    .Description("TODO: Move .deb creation into this task")
    ;

Task("Pack-RedHatPackage")
    .IsDependentOn("Pack-LinuxPackages-Legacy")
    .Description("TODO: Move .rpm creation into this task")
    ;

Task("Pack-WindowsInstallers")
    .Description("Packs the Windows .msi files.")
    .IsDependentOn("Build-Windows")
    .Does(() =>
    {
        CreateDirectory($"{artifactsDir}/msi");

        PackWindowsInstaller(PlatformTarget.x64);
        PackWindowsInstaller(PlatformTarget.x86);
    });

Task("Pack-CrossPlatformBundle")
    .Description("Packs the cross-platform Tentacle.nupkg used by Octopus Server to dynamically upgrade Tentacles.")
    .IsDependentOn("Build-Windows") // for the Octopus.Tentacle.Upgrader binary
    .IsDependentOn("Pack-WindowsInstallers")    // for the .msi files (Windows)
    .IsDependentOn("Pack-DebianPackage")    // for the .deb
    .IsDependentOn("Pack-RedHatPackage")    // for the .rpm
    .IsDependentOn("Pack-WindowsZips")  // for the .zip files (Windows)
    .IsDependentOn("Pack-LinuxTarballs")    // for the .tar.gz bundles (Linux)
    .IsDependentOn("Pack-OSXTarballs")  // for the .tar.gz bundle (OS X)
    .Does(() => {
        CreateDirectory($"{artifactsDir}/nuget");

        var workingDir = $"{buildDir}/Octopus.Tentacle.CrossPlatformBundle";
        CreateDirectory(workingDir);

        var debAMD64PackageFilename = ConstructDebianPackageFilename("tentacle", versionInfo, "amd64");
        var debARM64PackageFilename = ConstructDebianPackageFilename("tentacle", versionInfo, "arm64");
        var debARM32PackageFilename = ConstructDebianPackageFilename("tentacle", versionInfo, "armhf");

        var rpmARM64PackageFilename = ConstructRedHatPackageFilename("tentacle", versionInfo, "aarch64");
        var rpmARM32PackageFilename = ConstructRedHatPackageFilename("tentacle", versionInfo, "armv7hl");
        var rpmx64PackageFilename = ConstructRedHatPackageFilename("tentacle", versionInfo, "x86_64");

        CopyFiles($"./source/Octopus.Tentacle.CrossPlatformBundle/Octopus.Tentacle.CrossPlatformBundle.nuspec", workingDir);
        CopyFile($"{artifactsDir}/msi/Octopus.Tentacle.{versionInfo.FullSemVer}.msi", $"{workingDir}/Octopus.Tentacle.msi");
        CopyFile($"{artifactsDir}/msi/Octopus.Tentacle.{versionInfo.FullSemVer}-x64.msi", $"{workingDir}/Octopus.Tentacle-x64.msi");
        CopyFiles($"{buildDir}/Octopus.Tentacle.Upgrader/net452/win/*", workingDir);
        CopyFile($"{artifactsDir}/deb/{debAMD64PackageFilename}", $"{workingDir}/{debAMD64PackageFilename}");
        CopyFile($"{artifactsDir}/deb/{debARM64PackageFilename}", $"{workingDir}/{debARM64PackageFilename}");
        CopyFile($"{artifactsDir}/deb/{debARM32PackageFilename}", $"{workingDir}/{debARM32PackageFilename}");
        CopyFile($"{artifactsDir}/rpm/{rpmARM64PackageFilename}", $"{workingDir}/{rpmARM64PackageFilename}");
        CopyFile($"{artifactsDir}/rpm/{rpmARM32PackageFilename}", $"{workingDir}/{rpmARM32PackageFilename}");
        CopyFile($"{artifactsDir}/rpm/{rpmx64PackageFilename}", $"{workingDir}/{rpmx64PackageFilename}");

        foreach (var framework in frameworks)
        {
            foreach (var runtimeId in runtimeIds)
            {
                if (runtimeId == "win" && framework != "net452"
                 || runtimeId != "win" && framework == "net452") continue;  // General exclusion of net452+ (not Windows, and only the AnyCPU runtime id)

                var fileExtension = runtimeId.StartsWith("win") ? "zip" : "tar.gz";
                CopyFile($"{artifactsDir}/zip/tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.{fileExtension}", $"{workingDir}/tentacle-{framework}-{runtimeId}.{fileExtension}");
            }
        }

        // Assert that some key files exist.
        AssertFileExists($"{workingDir}/Octopus.Tentacle.msi");
        AssertFileExists($"{workingDir}/Octopus.Tentacle-x64.msi");
        AssertFileExists($"{workingDir}/Octopus.Tentacle.Upgrader.exe");
        AssertFileExists($"{workingDir}/{debAMD64PackageFilename}");
        AssertFileExists($"{workingDir}/{debARM64PackageFilename}");
        AssertFileExists($"{workingDir}/{debARM32PackageFilename}");
        AssertFileExists($"{workingDir}/{rpmARM64PackageFilename}");
        AssertFileExists($"{workingDir}/{rpmARM32PackageFilename}");
        AssertFileExists($"{workingDir}/{rpmx64PackageFilename}");

        RunProcess("dotnet", $"tool run dotnet-octo pack --id=Octopus.Tentacle.CrossPlatformBundle --version={versionInfo.FullSemVer} --basePath={workingDir} --outFolder={artifactsDir}/nuget");
    });

Task("Pack-Windows")
    .Description("Packs all the Windows targets.")
    .IsDependentOn("Pack-WindowsZips")
    .IsDependentOn("Pack-ChocolateyPackage")
    .IsDependentOn("Pack-WindowsInstallers")
    ;

Task("Pack-Linux")
    .Description("Packs all the Linux targets.")
    .IsDependentOn("Pack-LinuxTarballs")
    .IsDependentOn("Pack-DebianPackage")
    .IsDependentOn("Pack-RedHatPackage")
    ;

Task("Pack-OSX")
    .Description("Packs all the OS/X targets.")
    .IsDependentOn("Pack-OSXTarballs")
    ;

Task("Pack")
    .Description("Pack all the artifacts. Notional task - running this on a single host is possible but cumbersome.")
    .IsDependentOn("Pack-CrossPlatformBundle")
    .IsDependentOn("Pack-Windows")
    .IsDependentOn("Pack-Linux")
    .IsDependentOn("Pack-OSX")
    ;

// This block defines tasks looking like this:
//
// Task("Test-<framework>-<runtimeId>")
//
// We dynamically define test tasks based on the cross-product of frameworks and runtimes.
// We do this rather than attempting to have a single "Test" task because there's no feasible way to actually run
// all of the different framework/runtime combinations on a single host. Notable examples would be
// net452/win-anycpu and netcoreapp3.1/linux-musl-x64, or anything linux-x64 versus linux-arm64.
foreach (var framework in frameworks)
{
    foreach (var runtimeId in runtimeIds )
    {
        if (runtimeId == "win" && framework != "net452" //win runtime id can only do net452
         || runtimeId != "win" && framework == "net452") continue; //others can't do net452

        var testTaskName = $"Test-{framework}-{runtimeId}";
        var buildTaskName = $"Build-{framework}-{runtimeId}";

        Task(testTaskName)
            .IsDependentOn(buildTaskName)
            .Description($"Runs the test suite for {framework}/{runtimeId}")
            .Does(() => {
                RunTestSuiteFor(framework, runtimeId);
            });
    }
}

var testLinuxPackagesTask = Task("Test-LinuxPackages")
    .Description("Tests installing the .deb and .rpm packages onto all of the Linux target distributions.")
    .Does(() => {
        InTestSuite("Test-LinuxPackages", () => {
            foreach (var testConfiguration in testOnLinuxDistributions)
            {
                var framework = testConfiguration[0];
                var runtimeId = testConfiguration[1];
                var dockerImage = testConfiguration[2];
                var packageType = testConfiguration[3];

                RunLinuxPackageTestsFor(framework, runtimeId, dockerImage, packageType);
            }
        });
    });


Task("Copy-ToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("Pack-CrossPlatformBundle")
    .Description("If not running on a build agent, this step copies the relevant built artifacts to the local packages cache.")
    .Does(() =>
    {
    versionInfo.FullSemVer = "6.0.544-MissedTheMark-Bug-M";
        CreateDirectory(localPackagesDir);
        CopyFileToDirectory(Path.Combine(artifactsDir, $"Tentacle.{versionInfo.FullSemVer}.nupkg"), localPackagesDir);
    });

Task("Default")
    .IsDependentOn("Pack")
    .IsDependentOn("Copy-ToLocalPackages")
    ;


//////////////////////////////////////////////////////////////////////
// IMPLEMENTATION DETAILS
//////////////////////////////////////////////////////////////////////

private string DeriveGitBranch()
{
    var branch = EnvironmentVariable("OCTOVERSION_CurrentBranch");
    if (string.IsNullOrEmpty(branch))
    {
        Warning("Git branch not available from environment variable. Attempting to work it out for ourselves. DANGER: Don't rely on this on your build server!");
        if (TeamCity.IsRunningOnTeamCity)
        {
            var message = "Git branch not available from environment variable";
            Console.WriteLine($"##teamcity[message text='{message}' status='FAILURE']");
            throw new NotSupportedException(message);
        }

        branch = "refs/heads/" + RunProcessAndGetOutput("git", "rev-parse --abbrev-ref HEAD");
    }

    return branch;
}

private VersionInfo DeriveVersionInfo() {

    var existingFullSemVer = EnvironmentVariable("OCTOVERSION_FullSemVer");
    if (string.IsNullOrEmpty(existingFullSemVer))
    {
        var branch = DeriveGitBranch();
        Environment.SetEnvironmentVariable("OCTOVERSION_CurrentBranch", branch);
    }

    var octoVersionArgs = TeamCity.IsRunningOnTeamCity ? "--OutputFormats:0=Console --OutputFormats:1=TeamCity" : "--OutputFormats:0=Console";
    RunProcess("dotnet", $"tool run octoversion {octoVersionArgs}");

    var versionJson = RunProcessAndGetOutput("dotnet", $"tool run octoversion --OutputFormats:0=Json");
    var version = DeserializeJson<VersionInfo>(versionJson);

    Information("Building OctopusTentacle {0}", version.FullSemVer);
    return version;
}

private void InBlock(string block, Action action)
{
    if (TeamCity.IsRunningOnTeamCity)
        TeamCity.WriteStartBlock(block);
    else
        Information($"Starting {block}");

    try
    {
        action();
    }
    finally
    {
        if (TeamCity.IsRunningOnTeamCity)
            TeamCity.WriteEndBlock(block);
        else
            Information($"Finished {block}");
    }
}

private void InTestSuite(string testSuite, Action action)
{
    if (TeamCity.IsRunningOnTeamCity) Console.WriteLine($"##teamcity[testSuiteStarted name='{testSuite}']");

    try
    {
        action();
    }
    finally
    {
        if (TeamCity.IsRunningOnTeamCity) Console.WriteLine($"##teamcity[testSuiteFinished name='{testSuite}']");
    }
}

private void InTest(string test, Action action)
{
    var startTime = DateTimeOffset.UtcNow;

    try
    {
        if (TeamCity.IsRunningOnTeamCity) Console.WriteLine($"##teamcity[testStarted name='{test}' captureStandardOutput='true']");
        action();
    }
    catch (Exception ex)
    {
        if (TeamCity.IsRunningOnTeamCity) Console.WriteLine($"##teamcity[testFailed name='{test}' message='{ex.Message}']");
        Error(ex.ToString());
    }
    finally
    {
        var finishTime = DateTimeOffset.UtcNow;
        var elapsed = finishTime - startTime;
        if (TeamCity.IsRunningOnTeamCity) Console.WriteLine($"##teamcity[testFinished name='{test}' duration='{elapsed.TotalMilliseconds}']");
    }
}

private void RestoreFileOnCleanup(string file)
{
    var contents = System.IO.File.ReadAllBytes(file);
    cleanups.Add(() => {
        Information("Restoring {0}", file);
        System.IO.File.WriteAllBytes(file, contents);
    });
}

private void UpdateMsiProductVersion(string productWxs)
{
    var xmlDoc = new XmlDocument();
    xmlDoc.Load(productWxs);

    var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
    nsmgr.AddNamespace("wi", "http://schemas.microsoft.com/wix/2006/wi");

    var product = xmlDoc.SelectSingleNode("//wi:Product", nsmgr);
    product.Attributes["Version"].Value = versionInfo.MajorMinorPatch;
    xmlDoc.Save(productWxs);
}

private void GenerateMsiInstallerContents(string installerDir)
{
    InBlock("Running HEAT to generate the installer contents...", () => {
        var harvestDirectory = Directory(installerDir);
        var harvestFile = "./installer/Octopus.Tentacle.Installer/Tentacle.Generated.wxs";
        RestoreFileOnCleanup(harvestFile);

        var heatSettings = new HeatSettings {
            NoLogo = true,
            GenerateGuid = true,
            SuppressFragments = true,
            SuppressRootDirectory = true,
            SuppressRegistry = true,
            SuppressUniqueIds = true,
            ComponentGroupName = "TentacleComponents",
            PreprocessorVariable = "var.TentacleSource",
            DirectoryReferenceId = "INSTALLLOCATION"
        };

        WiXHeat(harvestDirectory, File(harvestFile), WiXHarvestType.Dir, heatSettings);
    });
}

private void BuildMsiInstallerForPlatform(PlatformTarget platformTarget)
{
    InBlock($"Building {platformTarget} installer", () => {
        MSBuild("./installer/Octopus.Tentacle.Installer/Octopus.Tentacle.Installer.wixproj", settings =>
            settings
                .SetConfiguration("Release")
                .WithProperty("AllowUpgrade", "True")
                .SetVerbosity(verbosity)
                .SetPlatformTarget(platformTarget)
                .WithTarget("build")
        );
        var builtMsi = File($"./installer/Octopus.Tentacle.Installer/bin/{platformTarget}/Octopus.Tentacle.msi");

        SignAndTimeStamp(builtMsi);

        var platformStr = platformTarget == PlatformTarget.x64
            ? "-x64"
            : "";

        var artifactDestination = $"{artifactsDir}/msi/Octopus.Tentacle.{versionInfo.FullSemVer}{platformStr}.msi";

        MoveFile(builtMsi, File(artifactDestination));
    });
}

private void PackWindowsInstaller(PlatformTarget platformTarget)
{
    var platformPath = platformTarget == PlatformTarget.x64 ? "win-x64" : "win-x86";
    var installerDir = $"{buildDir}/Installer/{platformPath}";
    CreateDirectory(installerDir);

    CopyFiles($"{buildDir}/Tentacle/net452/win/*", installerDir);
    CopyFiles($"{buildDir}/Octopus.Manager.Tentacle/net452/win/*", installerDir);
    CopyFiles("scripts/Harden-InstallationDirectory.ps1", installerDir);

    GenerateMsiInstallerContents(installerDir);
    BuildMsiInstallerForPlatform(platformTarget);
}

private void CreateLinuxPackages(string runtimeId)
{
    if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SIGN_PRIVATE_KEY")) )
    {
        throw new Exception("This build requires environment variables `SIGN_PRIVATE_KEY` (in a format gpg1 can import)"
            + " and `SIGN_PASSPHRASE`, which are used to sign the .rpm.");
    }

    //TODO It's probable that the .deb and .rpm package layouts will be different - and potentially _should already_ be different.
    // We're approaching this with the assumption that we'll split .deb and .rpm creation soon, which means that we'll create a separate
    // filesystem layout for each of them. Using .deb for now; expecting to replicate that soon for .rpm.
    var debBuildDir = $"{buildDir}/deb/{runtimeId}";
    CreateDirectory($"{debBuildDir}");
    CreateDirectory($"{debBuildDir}/scripts");
    CreateDirectory($"{debBuildDir}/output");

    // Use fully-qualified paths here so that the bind mount points work correctly.
    CopyFiles($"./linux-packages/packaging-scripts/*", $"{debBuildDir}/scripts/");
    var scriptsBindMountPoint = new System.IO.DirectoryInfo($"{debBuildDir}/scripts").FullName;
    var inputBindMountPoint = new System.IO.DirectoryInfo($"{buildDir}/zip/netcoreapp3.1/{runtimeId}/tentacle").FullName;
    var outputBindMountPoint = new System.IO.DirectoryInfo($"{debBuildDir}/output").FullName;

    DockerPull("docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest");
    DockerRunWithoutResult(new DockerContainerRunSettings {
        Rm = true,
        Tty = true,
        Env = new string[] {
            $"VERSION={versionInfo.FullSemVer}",
            "INPUT_PATH=/input",
            "OUTPUT_PATH=/output",
            "SIGN_PRIVATE_KEY",
            "SIGN_PASSPHRASE"
        },
        Volume = new string[] {
            $"{scriptsBindMountPoint}:/scripts",
            $"{inputBindMountPoint}:/input",
            $"{outputBindMountPoint}:/output"
        }
    }, "docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages:latest", $"bash /scripts/package.sh {runtimeId}");
}

private void RunLinuxPackageTestsFor(string framework, string runtimeId, string dockerImage, string packageType)
{
    InTest($"{framework}/{runtimeId}/{dockerImage}/{packageType}", () => {
        string archSuffix = null;
        if (packageType == "deb")
        {
            if (runtimeId == "linux-x64") archSuffix = "_amd64";
        } else if (packageType == "rpm")
        {
            if (runtimeId == "linux-x64") archSuffix = ".x86_64";
        }
        if (string.IsNullOrEmpty(archSuffix)) throw new NotSupportedException();

        var packageFileSpec = $"_artifacts/{packageType}/*{archSuffix}.{packageType}";
        Information($"Searching for files in {packageFileSpec}");
        var packageFile = new System.IO.FileInfo(GetFiles(packageFileSpec).AsEnumerable().Single().FullPath);
        Information($"Testing Linux package file {packageFile.Name}");

        var testScriptsBindMountPoint = new System.IO.DirectoryInfo($"linux-packages/test-scripts").FullName;
        var artifactsBindMountPoint = new System.IO.DirectoryInfo($"_artifacts").FullName;

        DockerPull(dockerImage);
        DockerRunWithoutResult(new DockerContainerRunSettings {
            Rm = true,
            Tty = true,
            Env = new string[] {
                $"VERSION={versionInfo.FullSemVer}",
                "INPUT_PATH=/input",
                "OUTPUT_PATH=/output",
                "SIGN_PRIVATE_KEY",
                "SIGN_PASSPHRASE",
                "REDHAT_SUBSCRIPTION_USERNAME",
                "REDHAT_SUBSCRIPTION_PASSWORD",
                $"BUILD_NUMBER={versionInfo.FullSemVer}"
            },
            Volume = new string[] {
                $"{testScriptsBindMountPoint}:/test-scripts:ro",
                $"{artifactsBindMountPoint}:/artifacts:ro"
            }
        }, dockerImage, $"bash /test-scripts/test-linux-package.sh /artifacts/{packageType}/{packageFile.Name}");
    });
}

// We need to use tar directly, because .NET utilities aren't able to preserve the file permissions
// Importantly, the Tentacle executable needs to be +x in the tar.gz file
private void TarGZipCompress(string inputDirectory, string filespec, string outputDirectory, string outputFile) {

    var inputMountPoint = new System.IO.DirectoryInfo(inputDirectory).FullName;
    var outputMountPoint = new System.IO.DirectoryInfo(outputDirectory).FullName;

    DockerRunWithoutResult(new DockerContainerRunSettings {
        Rm = true,
        Tty = true,
        Volume = new string[] {
            $"{inputMountPoint}:/input",
            $"{outputMountPoint}:/output",
        }
    }, "debian", $"tar -C /input -czvf /output/{outputFile} {filespec} --preserve-permissions");
}

// note: Doesn't check if existing signatures are valid, only that one exists
// source: https://blogs.msdn.microsoft.com/windowsmobile/2006/05/17/programmatically-checking-the-authenticode-signature-on-a-file/
private bool HasAuthenticodeSignature(FilePath fileInfo)
{
    try
    {
        X509Certificate.CreateFromSignedFile(fileInfo.FullPath);
        return true;
    }
    catch
    {
        return false;
    }
}

private void SignAndTimeStamp(params string[] paths)
{
    var allFiles = new List<FilePath>();
    foreach (var path in paths)
    {
        var files = GetFiles(path);
        allFiles.AddRange(files);
    }

    SignAndTimeStamp(allFiles.ToArray());
}

private void SignAndTimeStamp(params FilePath[] filePaths)
{
    InBlock("Signing and timestamping...", () => {

        foreach (var filePath in filePaths) {
            if (!FileExists(filePath)) throw new Exception($"File {filePath} does not exist");
            var fileInfo = new System.IO.FileInfo(filePath.FullPath);

            if (fileInfo.IsReadOnly) {
                InBlock($"{filePath.FullPath} is readonly. Making it writeable.", () => {
                    fileInfo.IsReadOnly = false;
                });
            }
        }

        if (string.IsNullOrEmpty(keyVaultUrl) && string.IsNullOrEmpty(keyVaultAppId) && string.IsNullOrEmpty(keyVaultAppSecret) && string.IsNullOrEmpty(keyVaultCertificateName))
        {
            Information("Signing files using signtool and the self-signed development code signing certificate.");
            SignWithSignTool(filePaths);
        }
        else
        {
            Information("Signing files using azuresigntool and the production code signing certificate.");
            SignWithAzureSignTool(filePaths);
        }
    });
}

private void SignWithAzureSignTool(IEnumerable<FilePath> files, string display = "", string displayUrl = "")
{
    var signArguments = new ProcessArgumentBuilder()
        .Append("sign")
        .Append("--azure-key-vault-url").AppendQuoted(keyVaultUrl)
        .Append("--azure-key-vault-client-id").AppendQuoted(keyVaultAppId)
        .Append("--azure-key-vault-client-secret").AppendQuotedSecret(keyVaultAppSecret)
        .Append("--azure-key-vault-certificate").AppendQuoted(keyVaultCertificateName)
        .Append("--file-digest sha256");

    if (!string.IsNullOrEmpty(display))
    {
        signArguments
            .Append("--description").AppendQuoted(display)
            .Append("--description-url").AppendQuoted(displayUrl);
    }

    foreach (var file in files)
    {
        signArguments.AppendQuoted(file.FullPath);
    }

    var azureSignToolPath = Context.MakeAbsolute(Context.File("./tools/azuresigntool.exe"));
    Information($"Executing: {azureSignToolPath} {signArguments.RenderSafe()}");
    RunProcess(azureSignToolPath.ToString(), signArguments.Render());

    Information($"Finished signing {files.Count()} files.");
}

private void SignWithSignTool(IEnumerable<FilePath> files, string display = "", string displayUrl = "")
{
    var lastException = default(Exception);
    var signSettings = new SignToolSignSettings
    {
        CertPath = File(signingCertificatePath),
        Password = signingCertificatePassword,
        DigestAlgorithm = SignToolDigestAlgorithm.Sha256,
        Description = "Octopus Tentacle Agent",
        DescriptionUri = new Uri("http://octopus.com")
    };

    foreach (var url in signingTimestampUrls)
    {
        InBlock($"Trying to time stamp using {url}", () => {
            signSettings.TimeStampUri = new Uri(url);
            try
            {
                Sign(files, signSettings);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        });

        if (lastException == null) return;
    }

    throw(lastException);
}

private string RunProcessAndGetOutput(string fileName, string args) {
    var stdoutStringBuilder = new StringBuilder();
    var settings = new ProcessSettings() {
        Arguments = args,
        RedirectStandardOutput = true,
        RedirectedStandardOutputHandler = s => { stdoutStringBuilder.Append(s); return s; }
     };

	var exitCode = StartProcess(fileName, settings);
    var stdout = stdoutStringBuilder.ToString();

	if(exitCode != 0)
	{
		var safeArgs = settings.Arguments.RenderSafe();
		throw new Exception($"{fileName} {safeArgs} failed with {exitCode} exitcode.");
	}

    return stdout;
}

private void RunProcess(string fileName, string args)
{
    var settings = new ProcessSettings() {
        Arguments = args
     };

	var exitCode = StartProcess(fileName, settings);

	if(exitCode != 0)
	{
		var safeArgs = settings.Arguments.RenderSafe();
		throw new Exception($"{fileName} {safeArgs} failed with {exitCode} exitcode.");
	}
}

private void RunBuildFor(string framework, string runtimeId)
{
    // 1. Build everything
    // 2. If Windows, sign the binaries
    // 3. Publish everything. This avoids signing binaries twice.
    var configuration = $"Release-{framework}-{runtimeId}";

    DotNetCoreBuild("./source/Tentacle.sln", new DotNetCoreBuildSettings {
        Configuration = configuration,
        Framework = framework,
        MSBuildSettings = new DotNetCoreMSBuildSettings {
            MaxCpuCount = 256
        },
        NoRestore = true,
        Runtime = runtimeId
    });

    Information(configuration);
    DotNetCorePublish("./source/Tentacle.sln",
        new DotNetCorePublishSettings
        {
            ArgumentCustomization = args => args.Append($"/p:Version={versionInfo.FullSemVer}"),
            Configuration = configuration,
            Framework = framework,
            NoBuild = true,
            NoRestore = true,
            Runtime = runtimeId
        }
    );

    if (runtimeId.StartsWith("win"))
    {
        // Sign any unsigned libraries that Octopus Deploy authors so that they play nicely with security scanning tools.
        // Refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
        // Decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
        var windowsOnlyBuiltFileSpec = $"{buildDir}/**/{framework}/{runtimeId}/**";
        var filesToSign =
            GetFiles(
                $"{windowsOnlyBuiltFileSpec}/**/Octo*.exe",
                $"{windowsOnlyBuiltFileSpec}/**/Octo*.dll",
                $"{windowsOnlyBuiltFileSpec}/**/Tentacle.exe",
                $"{windowsOnlyBuiltFileSpec}/**/Tentacle.dll",
                $"{windowsOnlyBuiltFileSpec}/**/Halibut.dll",
                $"{windowsOnlyBuiltFileSpec}/**/Nuget.*.dll"
            )
            .Where(f => !HasAuthenticodeSignature(f))
            .Select(f => f.FullPath)
            .ToArray();

        SignAndTimeStamp(filesToSign);
    }
}

private void RunTestSuiteFor(string framework, string runtimeId)
{
    CreateDirectory($"{artifactsDir}/teamcity");

    // We call dotnet test against the assemblies directly here because calling it against the .sln requires
    // the existence of the obj/* generated artifacts as well as the bin/* artifacts and we don't want to
    // have to shunt them all around the place.
    // By doing things this way, we can have a seamless experience between local and remote builds.
    var configuration = $"Release-{framework}-{runtimeId}";
    var testAssembliesPath = $"{buildDir}/Octopus.Tentacle.Tests/{framework}/{runtimeId}/*.Tests.dll";
    var testResultsPath = new System.IO.FileInfo($"{artifactsDir}/teamcity/TestResults-{framework}-{runtimeId}.xml").FullName;

    try
    {
        // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
        // when the test runner is being asked to build things, not when they're already built.
        // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
        DotNetCoreTest(testAssembliesPath, new DotNetCoreTestSettings
        {
            Framework = framework,
            ArgumentCustomization = args => args.Append($"--logger \"trx;LogFileName={testResultsPath}\"")
        });
    }
    catch (Exception ex)
    {
        Warning($"{ex.Message}: {ex}");
        // We want Cake to continue running even if tests fail. It's the responsibility of the build system to inspect
        // the test results files and assert on whether a failed test should fail the build. E.g. muted, ignored tests.
    }
}

private string ConstructDebianPackageFilename(string packageName, VersionInfo versionInfo, string architecture) {
    var filename = $"{packageName}_{versionInfo.FullSemVer}_{architecture}.deb";
    return filename;
}

private string ConstructRedHatPackageFilename(string packageName, VersionInfo versionInfo, string architecture) {
    var transformedVersion = versionInfo.FullSemVer.Replace("-", "_");
    var filename = $"{packageName}-{transformedVersion}-1.{architecture}.rpm";
    return filename;
}

void AssertFileExists(string fileSpec)
{
	var files = GetFiles(fileSpec);
	foreach (var file in files) Information($"Found file: {file}");
	if (!files.Any()) throw new Exception($"No file(s) matching {fileSpec} were found.");
}

RunTarget(target);