using CalendarsExportCsv.Utils;

namespace CalendarsExportCsv.Cli;

public sealed class CliOptions
{
    public string? SingleCalendar { get; private set; }
    public DateTime? DatesStart { get; private set; }
    public DateTime? DatesEnd { get; private set; }
    public string OutputDirectory { get; private set; } = Path.Combine(AppContext.BaseDirectory, "exports");
    public string CalendarsListFile { get; private set; } = "calendars.txt";
    public string TimeZoneId { get; private set; } = "America/Recife";

    public static CliOptions Parse(string[] argv)
    {
        var opts = new CliOptions();
        var args = new List<string>(argv ?? Array.Empty<string>());

        // flags
        for (int i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a.Equals("--outdir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                opts.OutputDirectory = Path.GetFullPath(args[i + 1]);
                args.RemoveAt(i + 1); args.RemoveAt(i); i -= 1; continue;
            }
            if (a.Equals("--tz", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                opts.TimeZoneId = args[i + 1].Trim();
                args.RemoveAt(i + 1); args.RemoveAt(i); i -= 1; continue;
            }
        }

        // positional: [0]=email? [1]=start? [2]=end?
        if (args.Count >= 1 && args[0].Contains("@")) { opts.SingleCalendar = args[0].Trim(); args.RemoveAt(0); }

        if (args.Count >= 1 && TextUtils.TryParseIsoDate(args[0], out var d1))
        {
            opts.DatesStart = d1.Date;
            args.RemoveAt(0);
        }
        if (args.Count >= 1 && TextUtils.TryParseIsoDate(args[0], out var d2))
        {
            opts.DatesEnd = d2.Date;
            args.RemoveAt(0);
        }

        return opts;
    }

    public List<string> ResolveCalendars()
    {
        if (!string.IsNullOrWhiteSpace(SingleCalendar))
            return new List<string> { SingleCalendar! };

        return FileUtils.LoadCalendars(CalendarsListFile);
    }
}
