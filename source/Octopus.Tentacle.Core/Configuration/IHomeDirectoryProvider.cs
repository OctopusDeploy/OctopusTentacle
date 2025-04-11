namespace Octopus.Tentacle.Configuration
{
    public interface IHomeDirectoryProvider
    {
        string? HomeDirectory { get; }
        string? ApplicationSpecificHomeDirectory { get; }
    }
}