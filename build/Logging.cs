// ReSharper disable RedundantUsingDirective
using System;
using Nuke.Common;
using Nuke.Common.CI.TeamCity;
using Serilog;

public static class Logging
{
    public static void InBlock(string block, Action action)
    {
        if (TeamCity.Instance != null)
        {
            TeamCity.Instance.OpenBlock(block);
        }
        else
        {
            Log.Information($"Starting {block}");
        }
        try
        {
            action();
        }
        finally
        {
            if (TeamCity.Instance != null)
            {
                TeamCity.Instance.CloseBlock(block);
            }
            else
            {
                Log.Information($"Finished {block}");
            }
        }
    }

    public static void InTest(string test, Action action)
    {
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            if (TeamCity.Instance != null) Console.WriteLine($"##teamcity[testStarted name='{test}' captureStandardOutput='true']");
            action();
        }
        catch (Exception ex)
        {
            if (TeamCity.Instance != null) Console.WriteLine($"##teamcity[testFailed name='{test}' message='{ex.Message}']");
            Log.Error(ex.ToString());
        }
        finally
        {
            var finishTime = DateTimeOffset.UtcNow;
            var elapsed = finishTime - startTime;
            if (TeamCity.Instance != null) Console.WriteLine($"##teamcity[testFinished name='{test}' duration='{elapsed.TotalMilliseconds}']");
        }
    }
}