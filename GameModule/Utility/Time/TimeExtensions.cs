using System.Globalization;

namespace Utility.Time
{
    public static class Time
    {
        // Stored format: UTC

        public static string databaseTimeFormat = "yyyyMMdd";
        public static string simpleTimeFormat = "yyyy-MM-dd";
        public static string fullTimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        
        private static string FormatedTime(this DateTime dateTime, string format)
        {
            return dateTime.ToString(format);
        }
        
        public static string SimpleUtcTime(this DateTime dateTime)
        {
            return dateTime.FormatedTime(simpleTimeFormat);
        }
        
        public static string FullUtcTime(this DateTime dateTime)
        {
            return dateTime.FormatedTime(fullTimeFormat);
        }
        
        public static string DatabaseTime(this DateTime dateTime)
        {
            return dateTime.FormatedTime(databaseTimeFormat);
        }
        
        public static string NowUtcTime()
        {
            return DateTime.UtcNow.FormatedTime(fullTimeFormat);
        }
        
        public static bool IsDateInRange(this DateTime now, string startStr, string endStr)
        {
            // 1. Safety: Use TryParse to prevent crashes on bad config strings
            if (!DateTime.TryParse(startStr, out DateTime startTime) ||
                !DateTime.TryParse(endStr, out DateTime endTime))
            {
                return false;
            }
            
            // 2. Comparison
            return now >= startTime && now <= endTime;

            // Return false (inactive) if dates are invalid/empty
        }
        
        public static bool IsDateInRangeInclusive(this DateTime now, string startStr, string endStr)
        {
            // 1. Safety: Use TryParse to prevent crashes on bad config strings
            if (!DateTime.TryParse(startStr, out DateTime startTime) ||
                !DateTime.TryParse(endStr, out DateTime endTime))
            {
                return false;
            }

            // 2. Logic: Handle "End of Day" inclusivity
            // If the end time is exactly midnight (00:00:00), the user likely provided 
            // just a date (e.g., "2025-07-30"). We should treat this as "End of that day".
            if (endTime.TimeOfDay == TimeSpan.Zero)
            {
                endTime = endTime.AddDays(1).AddTicks(-1); // Becomes 23:59:59.999
            }

            // 3. Comparison
            return now >= startTime && now <= endTime;

            // Return false (inactive) if dates are invalid/empty
        }
        
        public static bool IsDateInRangeNow(string startStr, string endStr)
        {
            return DateTime.UtcNow.IsDateInRange(startStr, endStr);
        }

        public static TimeSpan DateTimeDifference(DateTime start, DateTime end)
        {
            // Calculate the difference
            TimeSpan difference = end - start;
            return difference;
        }

        public static DateTime TryParseDate(this string date)
        {
            return DateTime.TryParse(date, out DateTime result) ? result : DateTime.UtcNow;
        }
        
        public static bool TryParseUtc(string time, out DateTime dtUtc)
        {
            if (DateTime.TryParseExact(time, fullTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                // Ensure Kind is UTC
                dtUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                return true;
            }

            if (DateTime.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
            {
                dtUtc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                return true;
            }

            dtUtc = DateTime.UtcNow;
            return false;
        }

        public static TimeSpan ParseTime(string timeStr)
        {
            // Supports formats like "14:30"
            return TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out TimeSpan result) 
                ? result : TimeSpan.Zero;
        }

        public static DateTime GetSafeMonthlyDate(int year, int month, int targetDay)
        {
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int validDay = Math.Min(targetDay, daysInMonth);
            return new DateTime(year, month, validDay, 0, 0, 0, DateTimeKind.Utc);
        }

        public static DateTime GetWindowEndFromStart(DateTime startUtc, TimeType type)
        {
            // Anchor to the start day's midnight UTC (the period that STARTED when the quest started)
            DateTime startMidnight = new DateTime(startUtc.Year, startUtc.Month, startUtc.Day, 0, 0, 0, DateTimeKind.Utc);

            switch (type)
            {
                case TimeType.Daily:
                    // End of the same day that started at startMidnight
                    return startMidnight.AddDays(1);

                case TimeType.Weekly:
                    // Weeks end at Saturday 00:00:00 UTC
                    // Compute the next Saturday after the start day's midnight.
                    int daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)startMidnight.DayOfWeek + 7) % 7;
                    if (daysUntilSaturday == 0) daysUntilSaturday = 7; // if starting on Saturday, give the full week
                    return startMidnight.AddDays(daysUntilSaturday);

                case TimeType.Monthly:
                    // End at the first day of the *next* month, 00:00:00 UTC
                    DateTime firstOfStartMonth = new DateTime(startMidnight.Year, startMidnight.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return firstOfStartMonth.AddMonths(1);

                default:
                    // Safe fallback: treat as daily
                    return startMidnight.AddDays(1);
            }
        }
        
        public static string NextTime(TimeType timeType)
        {
            DateTime now = DateTime.UtcNow;
            DateTime next;

            switch (timeType)
            {
                case TimeType.Daily:
                    // Next midnight UTC
                    next = now.Date.AddDays(1);
                    break;

                case TimeType.Weekly:
                    // Next Saturday 00:00:00 UTC
                    int daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
                    if (daysUntilSaturday == 0) daysUntilSaturday = 7; // if today is Saturday, go to next week
                    next = now.Date.AddDays(daysUntilSaturday);
                    break;

                case TimeType.Monthly:
                    // First day of next month 00:00:00 UTC
                    next = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                    break;

                default:
                    next = now.Date.AddDays(1);
                    break;
            }

            return next.ToString(fullTimeFormat);
        }
    }
}