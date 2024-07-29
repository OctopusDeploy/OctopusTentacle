namespace Octopus.Tentacle.Kubernetes.Tests.Integration;

public class HelmVersion1TestFixtureAttribute : TestFixtureAttribute
{
    public HelmVersion1TestFixtureAttribute():
        base("1.*.*")
    { }
}

public class HelmVersion2AlphaTestFixtureAttribute : TestFixtureAttribute
{
    public HelmVersion2AlphaTestFixtureAttribute():
        base("2.*.*-alpha")
    { }
}