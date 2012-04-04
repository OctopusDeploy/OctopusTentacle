using System;

public static class TimeSpanExtensions
{
    public static string FriendlyTime(this TimeSpan time)
    {
        if (time.TotalMinutes < 1) return time.TotalSeconds.ToString("n0") + " seconds";
        if (time.TotalHours < 1) return time.TotalMinutes.ToString("n0") + " minutes";
        if (time.TotalDays < 1) return time.TotalHours.ToString("n0") + " hours";
        return time.TotalDays.ToString("n0") + " days";
    }
}
