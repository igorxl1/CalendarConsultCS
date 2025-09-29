using CalendarsExportCsv.Cli;
using CalendarsExportCsv.Domain.Models;
using CalendarsExportCsv.Google;
using CalendarsExportCsv.Mapping;
using CalendarsExportCsv.Utils;
using Google.Apis.Calendar.v3;          // <-- CalendarService aqui
using Google;                           // <-- GoogleApiException aqui

namespace CalendarsExportCsv.Features.Export;

public sealed class Exporter
{
    private readonly CalendarService _service;   // tipo da Google API
    private readonly CliOptions _options;
    private readonly CalendarClient _client;

    public Exporter(CalendarService service, CliOptions options)
    {
        _service = service;
        _options = options;
        _client = new CalendarClient(service, options.TimeZoneId);
    }

    public async Task<List<CsvRow>> ExportAsync(List<string> calendarIds, DateTime timeMin, DateTime timeMax)
    {
        var rows = new List<CsvRow>();

        foreach (var calId in calendarIds)
        {
            try
            {
                Console.WriteLine($"\n➡️  Consultando: {calId}");
                var events = await _client.FetchEventsAsync(calId, timeMin, timeMax);

                foreach (var ev in events)
                {
                    var row = EventMapper.Map(calId, ev);
                    rows.Add(row);
                }

                Console.WriteLine($"   ✅ {rows.Count(r => r.Agenda == calId)} eventos acumulados dessa agenda.");
            }
            catch (global::Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.WriteLine($"   ⛔ Sem permissão para ler {calId}. Compartilhe a agenda com “Ver todos os detalhes do evento”.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao ler {calId}: {ex.Message}");
            }
        }

        return rows;
    }
}
