namespace PhoneStore.Business.Reporting;

public static class ReportingCalendar
{
    public const string TimeZoneId = "SE Asia Standard Time";
    public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static (DateTime StartUtc, DateTime EndExclusiveUtc, int OffsetMinutes) ToUtcRange(
        DateOnly fromDate, DateOnly toDate)
    {
        var localStart = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, TimeZone);
        var startOffset = TimeZone.GetUtcOffset(localStart);
        var endOffset = TimeZone.GetUtcOffset(localEnd);
        if (startOffset != endOffset)
            throw new InvalidOperationException("Reporting timezone offset changed inside the selected range.");
        return (startUtc, endUtc, checked((int)startOffset.TotalMinutes));
    }
}
