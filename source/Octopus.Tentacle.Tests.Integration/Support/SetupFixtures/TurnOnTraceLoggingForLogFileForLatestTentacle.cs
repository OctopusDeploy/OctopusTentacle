using System;
using System.IO;
using System.Threading;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.SetupFixtures
{
    public class TurnOnTraceLoggingForLogFileForLatestTentacle : ISetupFixture
    {
        private CancellationTokenSource cts = new();

        public void OneTimeSetUp(ILogger logger)
        {
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.DotNet6, logger);
            TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime.Framework48, logger);
        }

        void TryTurnOnTraceLoggingForTentacleRuntime(TentacleRuntime runtime, ILogger logger)
        {
            try
            {
                var exePath = TentacleExeFinder.FindTentacleExe(runtime);
                var exeFileInfo = new FileInfo(exePath);

                File.Copy(Path.Combine(exeFileInfo.DirectoryName!, "Tentacle.exe.nlog"), Path.Combine(exeFileInfo.DirectoryName!, "Tentacle.exe.nlog_original"));

                var nlogFileInfo = new FileInfo(Path.Combine(exeFileInfo.DirectoryName!, "Tentacle.exe.nlog"));

                var content = File.ReadAllText(nlogFileInfo.FullName);
                content = content.Replace("<logger name=\"*\" minlevel=\"Info\" writeTo=\"octopus-log-file\" />", "<logger name=\"*\" minlevel=\"Trace\" writeTo=\"octopus-log-file\" />");
                content = content.Replace("<logger name=\"*\" minlevel=\"Info\" maxLevel=\"Warn\" writeTo=\"stdout\" />", "<logger name=\"*\" minlevel=\"Trace\" maxLevel=\"Warn\" writeTo=\"stdout\" />");
                content = content.Replace("<variable name=\"messageLayout\" value=\"${message}${onexception:${newline}${exception:format=ToString}}\"/>", "<variable name=\"messageLayout\" value=\"[${level}] ${message}${onexception:${newline}${exception:format=ToString}}\"/>");
                
                File.WriteAllText(nlogFileInfo.FullName, content);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Unable to turn on Trace logging for {runtime}");
            }
        }
        
        public void OneTimeTearDown(ILogger logger)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
