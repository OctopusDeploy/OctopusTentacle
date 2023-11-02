using System;
using System.Text;
using Octopus.Tentacle.Util;

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
            for (int i = 0; i < count; i++)
            {
                Sleep(delay);
                Print(printString);
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
            //we add a 120s timout so the script actually completes if the text explodes and doesn't just run forever
            bashScript.AppendLine($@"
count = 0
until [ -f '{fileToWaitFor}' ] || [$count -gt 120];
do
    echo waiting
    sleep 1
    ((count++))
done
");
            windowsScript.AppendLine($@"
$count = 0
while (!(Test-Path '{fileToWaitFor}') -and $count -lt 120)
{{
    echo waiting
    Start-Sleep 1
    $count++
}}
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
    }
}
