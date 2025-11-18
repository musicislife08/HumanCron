using NodaTime;
using NodaTime.TimeZones;
using System;

namespace HumanCron.Quartz.Helpers;

/// <summary>
/// Helper to convert between NodaTime DateTimeZone and BCL TimeZoneInfo
/// Required for Quartz.NET interop which uses TimeZoneInfo
/// Uses NodaTime's built-in BclDateTimeZone for conversion
/// </summary>
internal static class TimeZoneConverter
{
    /// <summary>
    /// Convert NodaTime DateTimeZone to BCL TimeZoneInfo
    /// </summary>
    /// <param name="dateTimeZone">NodaTime timezone</param>
    /// <returns>Equivalent BCL TimeZoneInfo</returns>
    public static TimeZoneInfo ToTimeZoneInfo(DateTimeZone dateTimeZone)
    {
        ArgumentNullException.ThrowIfNull(dateTimeZone);

        // If it's already a BclDateTimeZone, extract the original
        if (dateTimeZone is BclDateTimeZone bclZone)
        {
            return bclZone.OriginalZone;
        }

        // Special case for UTC
        if (dateTimeZone == DateTimeZone.Utc)
        {
            return TimeZoneInfo.Utc;
        }

        // For TZDB zones, try to find by ID (works with IANA IDs on .NET 10)
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(dateTimeZone.Id);
        }
        catch
        {
            throw new InvalidOperationException(
                $"Could not convert DateTimeZone '{dateTimeZone.Id}' to TimeZoneInfo. " +
                $"Ensure the timezone is available on this system (.NET 10 with ICU support required).");
        }
    }

    /// <summary>
    /// Convert BCL TimeZoneInfo to NodaTime DateTimeZone
    /// </summary>
    /// <param name="timeZoneInfo">BCL timezone</param>
    /// <returns>Equivalent NodaTime DateTimeZone</returns>
    public static DateTimeZone ToDateTimeZone(TimeZoneInfo timeZoneInfo)
    {
        ArgumentNullException.ThrowIfNull(timeZoneInfo);

        // Use NodaTime's built-in BCL conversion
        return BclDateTimeZone.FromTimeZoneInfo(timeZoneInfo);
    }
}
