using System;

public static class TimeSpanExtensions
{
    public static string FriendlyTime(this TimeSpan time)
    {
        if (time.TotalMinutes < 1) return Format(time.TotalSeconds, "second");
        if (time.TotalHours < 1) return Format(time.TotalMinutes, "minute");
        if (time.TotalDays < 1) return Format(time.TotalHours, "hour");
        return Format(time.TotalDays, "day");
    }

    static string Format(double totalUnits, string unit)
    {
        var value = totalUnits.ToString("n0");
        if (value == "1")
        {
            return value + " " + unit;
        }
        else
        {
            return value + " " + unit + "s";            
        }
    }
}
