// ReSharper disable RedundantUsingDirective
using System;
using System.Diagnostics;
using System.Threading.Tasks;
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

    public static T InBlock<T>(string blockName, Func<T> action)
    {
        return InBlock(blockName, async () =>
        {
            await Task.CompletedTask;
            return action.Invoke();
        }).GetAwaiter().GetResult();
    }
    
    public static async Task<T> InBlock<T>(string blockName, Func<Task<T>> action)
    {
        var stopWatch = Stopwatch.StartNew();

        if (TeamCity.Instance != null)
        {
            TeamCity.Instance.OpenBlock(blockName);
        }
        else
        {
            Log.Information("{BlockName}{HeaderDelimiter}", blockName, new string('-', 30));
        }

        T result;
        try
        {
            result = await action();
            Log.Information("{BlockName} SUCCEEDED in {Elapsed:000}", blockName, stopWatch.Elapsed);
        }
        catch (Exception e)
        {
            Log.Error(e, "{BlockName} FAILED in {Elapsed:000}: {Message}", blockName, stopWatch.Elapsed, e.Message);
            throw;
        }
        finally
        {
            if (TeamCity.Instance != null)
            {
                TeamCity.Instance.CloseBlock(blockName);
            }
        }

        return result;
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
