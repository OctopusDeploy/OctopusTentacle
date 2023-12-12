using System.IO;
using System;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public static class TentacleNLogFile
    {
        public static void TryAdjustForRuntime(TentacleRuntime runtime, ILogger logger, Func<string, string> adjustLogContents)
        {
            try
            {
                var exePath = TentacleExeFinder.FindTentacleExe(runtime);
                var exeFileInfo = new FileInfo(exePath);
                var nlogFileInfo = new FileInfo(Path.Combine(exeFileInfo.DirectoryName!, "Tentacle.exe.nlog"));

                var content = File.ReadAllText(nlogFileInfo.FullName);
                content = adjustLogContents(content);
                File.WriteAllText(nlogFileInfo.FullName, content);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Unable to adjust Tentacle.exe.nlog for {runtime}");
            }
        }
    }
}