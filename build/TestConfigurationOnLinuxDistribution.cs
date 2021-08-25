using System;

public class TestConfigurationOnLinuxDistribution
{
    public TestConfigurationOnLinuxDistribution(string framework, string runtimeId, string dockerImage, string packageType)
    {
        Framework = framework;
        RuntimeId = runtimeId;
        DockerImage = dockerImage;
        PackageType = packageType;
    }

    public string Framework { get; }
    public string RuntimeId { get; }
    public string DockerImage { get; }
    public string PackageType { get; }
}
