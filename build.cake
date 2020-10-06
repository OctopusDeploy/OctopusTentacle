//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////
#module nuget:?package=Cake.DotNetTool.Module&version=0.4.0
#tool "dotnet:?package=OctoVersion.Tool&version=0.0.32"
#tool "nuget:?package=TeamCity.Dotnet.Integration&version=1.0.10"
#tool "nuget:?package=WiX&version=3.11.2"
#addin "nuget:?package=Cake.Compression&version=0.2.4"
#addin "nuget:?package=Cake.Docker&version=0.10.0"
#addin "nuget:?package=Cake.FileHelpers&version=3.2.1"
#addin "nuget:?package=Cake.Incubator&version=5.0.1"
#addin "nuget:?package=Cake.Json&version=5.2"
#addin "nuget:?package=Newtonsoft.Json&version=11.0.2"
#addin "nuget:?package=SharpZipLib&version=1.2.0"

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
var runtimeIds =  new [] { "win-x64", "linux-x64", "linux-musl-x64", "linux-arm64", "osx-x64" };
var frameworks = new [] { "net452", "netcoreapp3.1" };

var signingCertificatePath = Argument("signing_certificate_path", "./certificates/OctopusDevelopment.pfx");
var signingCertificatPassword = Argument("signing_certificate_password", "Password01!");

var gpgSigningCertificatePath = Argument("gpg_signing_certificate_path", "./certificates/octopus-privkey.asc");
var gpgSigningCertificatePassword = Argument("gpg_signing_certificate_password", "Password01!");

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
}

EnsureGitBranch();
var gitBranch = EnvironmentVariable("OCTOVERSION_CurrentBranch");
var versionInfo = DeriveVersionInfo();

// Keep this list in order by most likely to succeed
var signingTimestampUrls = new string[] {
    "http://timestamp.globalsign.com/scripts/timestamp.dll",
    "http://www.startssl.com/timestamp",
    "http://timestamp.comodoca.com/rfc3161",
    "http://timestamp.verisign.com/scripts/timstamp.dll",
    "http://tsa.starfieldtech.com"};


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
        if (framework == "net452" && runtimeId != "win-x64") continue;

        var taskName = $"Build-{framework}-{runtimeId}";
        Task(taskName)
            .IsDependentOn("Restore")
            .IsDependentOn("VersionAssemblies")
            .Description($"Builds and publishes for {framework}/{runtimeId}.")
            .Does(() => {
                RunBuildFor(framework, runtimeId);
            });

        // Include this task in the dependencies of whichever is the appropriate rolled-up build task for its operating system.
        if (runtimeId.StartsWith("win-"))
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

        var targetTrameworks = frameworks;
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("win-")).ToArray();

        foreach (var framework in targetTrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                Zip($"{buildDir}/Tentacle/{framework}/{runtimeId}/publish", $"{artifactsDir}/zip/tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.zip");
            }
        }
    });

Task("Pack-LinuxTarballs")
    .Description("Packs the Linux tarballs containing the published binaries.")
    .IsDependentOn("Build-Linux")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/zip");

        var targetTrameworks = frameworks.Where(f => f.StartsWith("netcore"));
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("linux-")).ToArray();

        foreach (var framework in targetTrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                GZipCompress($"{buildDir}/tentacle/{framework}/{runtimeId}/publish", $"{artifactsDir}/zip/tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.tar.gz");
            }
        }
    });

Task("Pack-OSXTarballs")
    .Description("Packs the OS/X tarballs containing the published binaries.")
    .IsDependentOn("Build-OSX")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/zip");

        var targetTrameworks = frameworks.Where(f => f.StartsWith("netcore"));
        var targetRuntimeIds = runtimeIds.Where(rid => rid.StartsWith("osx-")).ToArray();

        foreach (var framework in targetTrameworks)
        {
            foreach (var runtimeId in targetRuntimeIds)
            {
                GZipCompress($"{buildDir}/tentacle/{framework}/{runtimeId}/publish", $"{artifactsDir}/zip/tentacle-{versionInfo.FullSemVer}-{framework}-{runtimeId}.tar.gz");
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

        RunProcess("dotnet", $"octo pack --id=OctopusDeploy.Tentacle --version={versionInfo.FullSemVer} --basePath=./source/Chocolatey --outFolder={artifactsDir}/chocolatey");
    });


Task("Pack-LinuxPackages-Legacy")
    .IsDependentOn("Pack-LinuxTarballs")
    .Description("Legacy task until we can split creation of .rpm and .deb packages into their own tasks")
    .Does(() => {
        var runtimeIds = new [] { "linux-x64", "linux-arm64", "linux-musl-x64"};
        foreach (var runtimeId in runtimeIds)
        {
            CreateLinuxPackages(runtimeId);

            CreateDirectory($"{artifactsDir}/deb");
            CopyFiles($".{buildDir}/deb/{runtimeId}/out/*.deb", $"{artifactsDir}/deb");

            CreateDirectory($"{artifactsDir}/rpm");
            CopyFiles($".{buildDir}/deb/{runtimeId}/out/*.rpm", $"{artifactsDir}/rpm");
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

        var installerDir = $"{buildDir}/Installer";
        CreateDirectory(installerDir);

        CopyFiles($"{buildDir}/Tentacle/net452/win-x64/publish/*", installerDir);
        CopyFiles($"{buildDir}/Octopus.Manager.Tentacle/net452/win-x64/*", installerDir);

        GenerateMsiInstallerContents(installerDir);
        BuildMsiInstallerForPlatform(PlatformTarget.x64);
        BuildMsiInstallerForPlatform(PlatformTarget.x86);
    });

Task("Pack-CrossPlatformTentacleNuGetPackage")
    .Description("Packs the cross-platform Tentacle.nupkg used by Octopus Server to dynamically upgrade Tentacles.")
    .IsDependentOn("Pack-WindowsInstallers")
    .IsDependentOn("Pack-LinuxTarballs")
    .IsDependentOn("Pack-OSXTarballs")
    .Does(() => {
        CreateDirectory($"{artifactsDir}/nuget");

        var workingDir = $"{buildDir}/tentacle-upgrader";
        CreateDirectory(workingDir);
        CopyFiles($"./source/Octopus.Upgrader/Tentacle.spec", workingDir);
        CopyFiles($"{artifactsDir}/msi/Octopus.Tentacle.{versionInfo.FullSemVer}*.msi", workingDir); // Windows x86 and x64 .msi files
        CopyFiles($"{artifactsDir}/zip/tentacle-{versionInfo.FullSemVer}-*.tar.gz", workingDir);    // Linux and OS/X tarballs

        RunProcess("dotnet", $"octo pack --id=Tentacle --version={versionInfo.FullSemVer} --basePath={workingDir} --outFolder={artifactsDir}/nuget");
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
    .IsDependentOn("Pack-CrossPlatformTentacleNuGetPackage")
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
// net452/win-x64 and netcoreapp3.1/linux-musl-x64, or anything linux-x64 versus linux-arm64.
foreach (var framework in frameworks)
{
    foreach (var runtimeId in runtimeIds )
    {
        if (framework == "net452" && runtimeId != "win-x64") continue;

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

Task("Copy-ToLocalPackages")
    .WithCriteria(BuildSystem.IsLocalBuild)
    .IsDependentOn("Pack-CrossPlatformTentacleNuGetPackage")
    .Description("If not running on a build agent, this step copies the relevant built artifacts to the local packages cache.")
    .Does(() =>
    {
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

private void EnsureGitBranch()
{
    var branch = EnvironmentVariable("OCTOVERSION_CurrentBranch");
    if (!string.IsNullOrEmpty(branch)) return;

    Warning("Git branch not available from environment variable. Attempting to work it out for ourselves. DANGER: Don't rely on this on your build server!");
    branch = "refs/heads/" + RunProcessAndGetOutput("git", "branch --show-current");
    Environment.SetEnvironmentVariable("OCTOVERSION_CurrentBranch", branch);
}

private VersionInfo DeriveVersionInfo() {
    var octoVersionArgs = TeamCity.IsRunningOnTeamCity ? "--OutputFormats:0=Console --OutputFormats:1=TeamCity" : "--OutputFormats:0=Console";
    RunProcess("octoversion", $"{octoVersionArgs}");

    var versionJson = RunProcessAndGetOutput("octoversion", $"--OutputFormats:0=Json");
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
    CreateDirectory($"{debBuildDir}/input");
    CreateDirectory($"{debBuildDir}/input/scripts");
    CreateDirectory($"{debBuildDir}/output");

    CopyFiles("./scripts/configure-tentacle.sh", $"{debBuildDir}/input/scripts/");  //TODO this doesn't quite wire up yet.

    // Use fully-qualified paths here so that the bind mount points work correctly.
    var scriptsBindMountPoint = new System.IO.DirectoryInfo($"./scripts").FullName;
    var inputBindMountPoint = new System.IO.DirectoryInfo($"{buildDir}/Tentacle/netcoreapp3.1/{runtimeId}/publish").FullName;
    var outputBindMountPoint = new System.IO.DirectoryInfo($"{debBuildDir}/output").FullName;

    DockerRunWithoutResult(new DockerContainerRunSettings {
        Rm = true,
        Tty = true,
        Env = new string[] {
            $"VERSION={versionInfo.FullSemVer}",
            "BINARIES_PATH=/app/",
            "PACKAGES_PATH=/artifacts",
            "SIGN_PRIVATE_KEY",
            "SIGN_PASSPHRASE"
        },
        //TODO Impedance mismatch here with paths.
        Volume = new string[] {
            $"{scriptsBindMountPoint}:/scripts",
            $"{inputBindMountPoint}:/app",
            $"{outputBindMountPoint}:/artifacts"
        }
    }, "docker.packages.octopushq.com/octopusdeploy/tool-containers/tool-linux-packages", $"bash /scripts/package.sh {runtimeId}");
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

        var lastException = default(Exception);
        var signSettings = new SignToolSignSettings
        {
            CertPath = File(signingCertificatePath),
            Password = signingCertificatPassword,
            DigestAlgorithm = SignToolDigestAlgorithm.Sha256,
            Description = "Octopus Tentacle Agent",
            DescriptionUri = new Uri("http://octopus.com")
        };

        foreach (var url in signingTimestampUrls)
        {
            InBlock($"Trying to time stamp [{string.Join(Environment.NewLine, filePaths.Select(a => a.ToString()))}] using {url}", () => {
                signSettings.TimeStampUri = new Uri(url);
                try
                {
                    Sign(filePaths, signSettings);
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
    });
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

    if (runtimeId == "win-x64")
    {
        // check that any unsigned libraries, that Octopus Deploy authors, get signed to play nice with security scanning tools
        // refer: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1551655877004400
        // decision re: no signing everything: https://octopusdeploy.slack.com/archives/C0K9DNQG5/p1557938890227100
        var windowsOnlyBuiltFileSpec = $"{buildDir}/**/win-x64/**";
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

    // NOTE: Configuration, NoRestore, NoBuild and Runtime parameters are meaningless here as they only apply
    // when the test runner is being asked to build things, not when they're already built.
    // Framework is still relevant because it tells dotnet which flavour of test runner to launch.
    DotNetCoreTest(testAssembliesPath, new DotNetCoreTestSettings
    {
        Framework = framework,
        ArgumentCustomization = args => args.Append($"--logger \"trx;LogFileName={testResultsPath}\"")
    });
}

RunTarget(target);
