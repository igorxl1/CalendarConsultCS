using CalendarsExportCsv.Cli;
using CalendarsExportCsv.Domain;
using CalendarsExportCsv.Features.Export;
using CalendarsExportCsv.Google;
using CalendarsExportCsv.Utils;
using System.Formats.Asn1;

Console.OutputEncoding = System.Text.Encoding.UTF8;

try
{
    var options = CliOptions.Parse(args);
    var (timeMin, timeMax) = DateRange.Resolve(options.DatesStart, options.DatesEnd); // timeMax exclusivo
    var logEndInclusive = timeMax.AddDays(-1);

    Console.WriteLine($"🔎 Intervalo: {timeMin:yyyy-MM-dd} → {logEndInclusive:yyyy-MM-dd}");
    Console.WriteLine($"🕑 Timezone: {options.TimeZoneId}");
    Console.WriteLine($"💾 Saída: {options.OutputDirectory}");

    // resolve calendários
    var calendars = options.ResolveCalendars();
    if (calendars.Count == 0)
    {
        Console.WriteLine("❌ Nenhuma agenda válida. Informe um e-mail ou use um calendars.txt.");
        return;
    }

    // Google service
    var service = await GoogleAuth.CreateCalendarServiceAsync();

    // Export
    var exporter = new Exporter(service, options);
    var rows = await exporter.ExportAsync(calendars, timeMin, timeMax);

    // CSV
    var endInclusive = logEndInclusive;
    var csvName = CsvWriter.BuildCsvFileName(options.SingleCalendar, timeMin, endInclusive);
    var outPath = Path.Combine(options.OutputDirectory, csvName);
    Directory.CreateDirectory(options.OutputDirectory);
    File.WriteAllText(outPath, CsvWriter.ToCsv(rows), System.Text.Encoding.UTF8);

    Console.WriteLine($"\n✅ CSV gerado: {outPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro: {ex.Message}");
#if DEBUG
    Console.WriteLine(ex);
#endif
}
