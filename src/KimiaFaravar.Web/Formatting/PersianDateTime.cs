using System.Globalization;

namespace KimiaFaravar.Web.Formatting;

public static class PersianDateTime
{
    private static readonly PersianCalendar Calendar = new();
    private static readonly CultureInfo PersianCulture = CultureInfo.GetCultureInfo("fa-IR");
    private static readonly TimeZoneInfo IranTimeZone = FindIranTimeZone();

    public static string Format(DateTimeOffset value, bool includeSeconds = false)
    {
        var local = TimeZoneInfo.ConvertTime(value, IranTimeZone).DateTime;
        var result = string.Create(CultureInfo.InvariantCulture,
            $"{Calendar.GetYear(local):0000}/{Calendar.GetMonth(local):00}/{Calendar.GetDayOfMonth(local):00} {local.Hour:00}:{local.Minute:00}{(includeSeconds ? $":{local.Second:00}" : "")}");
        return ToPersianDigits(result);
    }

    public static string ServerToday()
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IranTimeZone).DateTime;
        var dayName = PersianCulture.DateTimeFormat.GetDayName(local.DayOfWeek);
        var date = string.Create(CultureInfo.InvariantCulture,
            $"{Calendar.GetYear(local):0000}/{Calendar.GetMonth(local):00}/{Calendar.GetDayOfMonth(local):00}");
        return $"{dayName} {ToPersianDigits(date)}";
    }

    public static string TodayDate() => FormatDate(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IranTimeZone).DateTime);

    public static string DaysAgoDate(int days) =>
        FormatDate(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, IranTimeZone).DateTime.AddDays(-days));

    public static bool TryParseRange(string? from, string? to, out DateTimeOffset fromUtc, out DateTimeOffset toExclusiveUtc)
    {
        fromUtc = default;
        toExclusiveUtc = default;
        if (!TryParseDate(from, out var fromLocal) || !TryParseDate(to, out var toLocal) || fromLocal > toLocal)
            return false;

        fromUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(fromLocal, IranTimeZone), TimeSpan.Zero);
        toExclusiveUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(toLocal.AddDays(1), IranTimeZone), TimeSpan.Zero);
        return true;
    }

    private static bool TryParseDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = ToLatinDigits(value.Trim()).Replace('-', '/');
        var parts = normalized.Split('/');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            return false;
        try
        {
            result = DateTime.SpecifyKind(Calendar.ToDateTime(year, month, day, 0, 0, 0, 0), DateTimeKind.Unspecified);
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private static string FormatDate(DateTime value) => ToPersianDigits(string.Create(CultureInfo.InvariantCulture,
        $"{Calendar.GetYear(value):0000}/{Calendar.GetMonth(value):00}/{Calendar.GetDayOfMonth(value):00}"));

    private static string ToLatinDigits(string value)
    {
        const string persian = "۰۱۲۳۴۵۶۷۸۹";
        const string arabic = "٠١٢٣٤٥٦٧٨٩";
        return string.Concat(value.Select(character =>
            persian.IndexOf(character) is var p && p >= 0 ? (char)('0' + p) :
            arabic.IndexOf(character) is var a && a >= 0 ? (char)('0' + a) : character));
    }

    private static TimeZoneInfo FindIranTimeZone()
    {
        foreach (var id in new[] { "Iran Standard Time", "Asia/Tehran" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        return TimeZoneInfo.Utc;
    }

    private static string ToPersianDigits(string value)
    {
        const string latin = "0123456789";
        const string persian = "۰۱۲۳۴۵۶۷۸۹";
        return string.Concat(value.Select(character => latin.IndexOf(character) is var index && index >= 0 ? persian[index] : character));
    }
}