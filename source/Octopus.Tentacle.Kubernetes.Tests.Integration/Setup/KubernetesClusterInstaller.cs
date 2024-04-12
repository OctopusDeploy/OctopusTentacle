﻿using System.Diagnostics;
using System.Reflection;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Setup.Tooling;
using Octopus.Tentacle.Kubernetes.Tests.Integration.Support.Logging;
using Octopus.Tentacle.Util;
using PlatformDetection = Octopus.Tentacle.CommonTestUtils.PlatformDetection;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Setup;

public class KubernetesClusterInstaller
{
    readonly string clusterName;
    readonly string kubeConfigName;

    readonly TemporaryDirectory tempDir;
    string kindExe = null!;
    string kubeCtlExe = null!;
    readonly ILogger logger;

    public string KubeConfigPath => Path.Combine(tempDir.DirectoryPath, kubeConfigName);

    public KubernetesClusterInstaller()
    {
        tempDir = new TemporaryDirectory();
        
        logger = new SerilogLoggerBuilder().Build();

        clusterName = $"tentacleint-{DateTime.Now:yyyyMMddhhmmss}";
        kubeConfigName = $"{clusterName}.config";
    }

    public async Task Install()
    {
        var kindDownloader = new KindDownloader(logger);
        kindExe = await kindDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);

        var kubectlDownloader = new KubeCtlDownloader(logger);
        kubeCtlExe = await kubectlDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);

        var configFilePath = await WriteFileToTemporaryDirectory("kind-config.yaml");

        var sw = new Stopwatch();
        sw.Restart();
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kindExe,
            //we give the cluster a unique name
            $"create cluster --name={clusterName} --config=\"{configFilePath}\" --kubeconfig=\"{kubeConfigName}\"",
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);

        sw.Stop();

        if (exitCode != 0)
        {
            logger.Error("Failed to create Kind Kubernetes cluster {ClusterName}", clusterName);
            throw new InvalidOperationException($"Failed to create Kind Kubernetes cluster {clusterName}");
        }
        
        logger.Information("Test cluster kubeconfig path: {Path}", KubeConfigPath);

        logger.Information("Created Kind Kubernetes cluster {ClusterName} in {ElapsedTime}", clusterName, sw.Elapsed);

        await SetLocalhostRouting();

        await InstallNfiCsiDriver();
    }

    async Task SetLocalhostRouting()
    {
        var filename = PlatformDetection.IsRunningOnNix ? "linux-network-routing.yaml" : "docker-desktop-network-routing.yaml";
        
        var manifestFilePath = await WriteFileToTemporaryDirectory(filename, "manifest.yaml");
        
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kubeCtlExe,
            //we give the cluster a unique name
            $"apply -f \"{manifestFilePath}\"--kubeconfig=\"{KubeConfigPath}\"",
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);
        
        if (exitCode != 0)
        {
            logger.Error("Failed to apply localhost routing to cluster {ClusterName}", clusterName);
            throw new InvalidOperationException($"Failed to apply localhost routing to cluster {clusterName}");
        }
    }

    async Task<string> WriteFileToTemporaryDirectory(string resourceFileName, string? outputFilename = null)
    {
        var asm = Assembly.GetExecutingAssembly();
        var valuesFileName = asm.GetManifestResourceNames().First(n => n.Contains(resourceFileName, StringComparison.OrdinalIgnoreCase));
        await using var resourceStream = asm.GetManifestResourceStream(valuesFileName)!;

        var filePath = Path.Combine(tempDir.DirectoryPath, outputFilename ?? resourceFileName);
        await using var file = File.Create(filePath);

        resourceStream.Seek(0, SeekOrigin.Begin);
        await resourceStream.CopyToAsync(file);

        return filePath;
    }

    async Task InstallNfiCsiDriver()
    {
        var helmDownloader = new HelmDownloader(logger);
        var helmPath = await helmDownloader.Download(tempDir.DirectoryPath, CancellationToken.None);

        //we need to perform a repo update in helm first
        // var exitCode = SilentProcessRunner.ExecuteCommand(
        //     helmPath,
        //     "repo update",
        //     tempDir.DirectoryPath,
        //     logger.Debug,
        //     logger.Information,
        //     logger.Error,
        //     CancellationToken.None);

        var installArgs = BuildNfsCsiDriverInstallArguments();
        var exitCode = SilentProcessRunner.ExecuteCommand(
            helmPath,
            installArgs,
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to install NFS CSI driver into cluster {clusterName}");
        }
    }

    string BuildNfsCsiDriverInstallArguments()
    {
        return string.Join(" ",
            "install",
            "--atomic",
            "--repo https://raw.githubusercontent.com/kubernetes-csi/csi-driver-nfs/master/charts",
            "--namespace kube-system",
            "--version v4.6.0",
            $"--kubeconfig \"{KubeConfigPath}\"",
            "csi-driver-nfs",
            "csi-driver-nfs"
        );
    }

    public void Dispose()
    {
        var exitCode = SilentProcessRunner.ExecuteCommand(
            kindExe,
            //delete the cluster for this test run
            $"delete cluster --name={clusterName}",
            tempDir.DirectoryPath,
            logger.Debug,
            logger.Information,
            logger.Error,
            CancellationToken.None);
        
        if (exitCode != 0)
        {
            logger.Error("Failed to delete Kind kubernetes cluster {ClusterName}", clusterName);
        }

        tempDir.Dispose();
    }
}