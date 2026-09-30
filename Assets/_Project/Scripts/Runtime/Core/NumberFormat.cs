using System;
using System.Globalization;

namespace IdleMart.Core
{
    /// <summary>Compact number formatting for the UI ("$1.25K", "1h 20m").</summary>
    public static class NumberFormat
    {
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T" };

        public static string Money(long value) => "$" + Short(value);

        /// <summary>Three significant digits with a K/M/B/T suffix above 9 999.</summary>
        public static string Short(double value)
        {
            var sign = value < 0 ? "-" : "";
            value = Math.Abs(value);
            if (value < 10_000) return sign + Math.Floor(value).ToString("#,0", CultureInfo.InvariantCulture);

            var index = 0;
            while (value >= 1000 && index < Suffixes.Length - 1)
            {
                value /= 1000;
                index++;
            }

            var format = value >= 100 ? "0" : value >= 10 ? "0.#" : "0.##";
            return sign + (Math.Floor(value * 100) / 100).ToString(format, CultureInfo.InvariantCulture) + Suffixes[index];
        }

        public static string Duration(TimeSpan span)
        {
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
            if (span.TotalMinutes >= 1) return $"{span.Minutes}m";
            return $"{Math.Max(0, span.Seconds)}s";
        }
    }
}
