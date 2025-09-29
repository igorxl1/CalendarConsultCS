namespace CalendarsExportCsv.Utils;

public static class FileUtils
{
    public static List<string> LoadCalendars(string path)
    {
        if (!File.Exists(path)) return new List<string>();
        return File.ReadAllLines(path)
                   .Select(l => l.Trim())
                   .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList();
    }
}
