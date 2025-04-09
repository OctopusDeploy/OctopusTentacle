using System;
using System.Text;
using Octopus.Tentacle.CommonTestUtils;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public class ScriptBuilder
    {
        private StringBuilder bashScript = new StringBuilder();
        private StringBuilder windowsScript = new StringBuilder();

        public ScriptBuilder Print(string stringToPrint)
        {
            bashScript.AppendLine($"echo \"{stringToPrint}\"");
            windowsScript.AppendLine($"echo \"{stringToPrint}\"");
            return this;
        }

        public ScriptBuilder Sleep(TimeSpan timeSpan)
        {
            bashScript.AppendLine($"sleep \"{timeSpan.TotalSeconds}\"");
            windowsScript.AppendLine($"Start-Sleep -Seconds \"{timeSpan.TotalSeconds}\"");
            return this;
        }

        public ScriptBuilder PrintNTimesWithDelay(string printString, int count, TimeSpan delay)
        {
            // TODO make this a for loop in bash and powershell
            for (var i = 0; i < count; i++)
            {
                Sleep(delay);
                Print(printString);
            }

            return this;
        }

        public ScriptBuilder PrintNTimesWithDelay(Func<int, string> outputBuilder, int count, TimeSpan delay)
        {
            // TODO make this a for loop in bash and powershell
            for (var i = 0; i < count; i++)
            {
                Sleep(delay);
                Print(outputBuilder(i));
            }

            return this;
        }

        public ScriptBuilder CreateFile(string file)
        {
            bashScript.AppendLine($@"
touch '{file}'
");
            windowsScript.AppendLine($@"
New-Item -type file '{file}'
");
            return this;
        }

        public ScriptBuilder WaitForFileToExist(string fileToWaitFor)
        {
            bashScript.AppendLine($@"
until [ -f '{fileToWaitFor}' ]
do
    echo waiting
    sleep 1
done
");
            windowsScript.AppendLine($@"
while (!(Test-Path '{fileToWaitFor}'))" + @"
{
    echo waiting
    Start-Sleep 1
}
");
            return this;
        }


        public ScriptBuilder PrintArguments()
        {
            bashScript.AppendLine(@"
for arg in ""$@""; do echo ""Argument: $arg""; done
");
            windowsScript.AppendLine(@"
ForEach ($arg in $args){
    write-output ""Argument: $arg""
}
");
            return this;
        }
        
        public ScriptBuilder PrintEnv()
        {
            bashScript.AppendLine(@"
printenv
");
            windowsScript.AppendLine(@"
SET
");
            return this;
        }

        public ScriptBuilder PrintFileContents(string file)
        {
            bashScript.AppendLine($@"
cat '{file}'
");
            windowsScript.AppendLine($@"
cat {file}
");
            return this;
        }

        public string BuildForCurrentOs()
        {
            return PlatformDetection.IsRunningOnWindows ? windowsScript.ToString() : bashScript.ToString();
        }

        public string BuildPowershellScript()
        {
            return windowsScript.ToString();
        }

        public string BuildBashScript()
        {
            return bashScript.ToString();
        }

        public ScriptBuilder ExitsWith(int exitCode)
        {
            bashScript.AppendLine($"exit {exitCode}");
            windowsScript.AppendLine($"Exit {exitCode}");
            return this;
        }
    }
}
