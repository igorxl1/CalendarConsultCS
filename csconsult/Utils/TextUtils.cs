using Google.Apis.Calendar.v3.Data;
using System.Text.RegularExpressions;

namespace CalendarsExportCsv.Utils;

public static class TextUtils
{
    public static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = Regex.Replace(html, "(?i)<br\\s*/?>", "\n");
        text = Regex.Replace(text, "<.*?>", " ");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }

    public static string NormalizePhone(string raw)
    {
        var digits = DigitsOnly(raw);
        if (digits.Length == 11) return $"({digits[..2]}) {digits[2]}{digits[3..7]}-{digits[7..]}";
        if (digits.Length == 10) return $"({digits[..2]}) {digits[2..6]}-{digits[6..]}";
        return digits;
    }

    public static string DigitsOnly(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    public static bool TryParseIsoDate(string text, out DateTime dt)
    {
        dt = default;
        return DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd",
                                      System.Globalization.CultureInfo.InvariantCulture,
                                      System.Globalization.DateTimeStyles.None, out dt)
            || DateTime.TryParse(text, System.Globalization.CultureInfo.CurrentCulture,
                                 System.Globalization.DateTimeStyles.None, out dt);
    }

    public static string? FormatEventDateTime(EventDateTime? edt)
    {
        if (edt == null) return "";
        if (edt.DateTimeDateTimeOffset.HasValue)
            return edt.DateTimeDateTimeOffset.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        if (!string.IsNullOrEmpty(edt.Date))
            return edt.Date; // all-day
        return "";
    }
}
