namespace Octopus.Pipelines.Runner.StepRunner;

/// <summary>
/// Copies the step runner into a directory, e.g. an <c>emptyDir</c> shared with the step's container. The step
/// runner's own image has nothing else in it, not even <c>cp</c>.
/// </summary>
static class Installer
{
    public const string FileName = "octopus-step-runner";

    public static int Install(string directory)
    {
        var self = Environment.ProcessPath;
        if (self is null)
        {
            Console.Error.WriteLine("Can't find the step runner's own executable.");
            return 1;
        }

        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, FileName);
        File.Copy(self, destination, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination, (UnixFileMode)0b111_101_101);
        }

        return 0;
    }
}
