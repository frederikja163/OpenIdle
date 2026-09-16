using System;

namespace Backend.Extensions;

internal static class DateTimeExtensions
{
    private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    extension(DateTime dateTime)
    {
        public long ToJs()
        {
            return (long)Math.Round(dateTime.Subtract(UnixEpoch).TotalMilliseconds);
        }
    }
}