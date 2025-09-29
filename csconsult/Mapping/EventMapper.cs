using CalendarsExportCsv.Domain.Models;
using CalendarsExportCsv.Utils;
using Google.Apis.Calendar.v3.Data;
using System.Text.Json;

namespace CalendarsExportCsv.Mapping;

public static class EventMapper
{
    public static CsvRow Map(string calId, Event ev)
    {
        var descPlain = TextUtils.StripHtml(ev.Description ?? "");
        var parsed = Parsing.DescriptionParser.Parse(descPlain);

        var attendees = ev.Attendees?.Select(a =>
            string.IsNullOrWhiteSpace(a.DisplayName) ? a.Email : $"{a.DisplayName} <{a.Email}>")
            .ToList() ?? new List<string>();

        var confPoints = BuildConferenceEntryPoints(ev);
        var attachments = ev.Attachments?.Select(att =>
            $"{att.Title} ({att.MimeType}) -> {att.FileUrl ?? att.IconLink ?? ""}")
            .ToList() ?? new List<string>();

        var extProps = CompactExtendedProps(ev);

        return new CsvRow
        {
            Agenda = calId,
            EventId = ev.Id ?? "",
            Titulo = ev.Summary ?? "",
            Local = ev.Location ?? "",
            HtmlLink = ev.HtmlLink ?? "",
            DataInicio = TextUtils.FormatEventDateTime(ev.Start),
            DataFim = TextUtils.FormatEventDateTime(ev.End),
            Timezone = ev.Start?.TimeZone ?? ev.End?.TimeZone ?? "",
            LinkMeet = ev.HangoutLink ?? "",
            ConferenceEntryPoints = string.Join(" | ", confPoints),

            Organizador = ev.Organizer?.DisplayName ?? ev.Organizer?.Email ?? "",
            CriadorNome = ev.Creator?.DisplayName ?? "",
            CriadorEmail = ev.Creator?.Email ?? "",
            Convidados = string.Join(" | ", attendees),

            EventoOcorrido = HasMeetRecording(ev) ? "Sim" : "Não",

            Anexos = string.Join(" | ", attachments),
            ExtendedProperties = extProps,

            ReservadoPor = parsed.ReservadoPor,
            EmailNaDescricao = parsed.Email,
            Telefones = string.Join(" / ", parsed.Telefones),

            CNPJ = parsed.CNPJs.FirstOrDefault() ?? "",
            CNPJs = string.Join(" | ", parsed.CNPJs),

            ID = parsed.ID,
            Etapa = parsed.Etapa,
            Etapas = string.Join(" | ", parsed.Etapas),

            UF = parsed.UF,

            Descricao = descPlain.Replace("\r", " ").Replace("\n", " ").Trim()
        };
    }

    private static List<string> BuildConferenceEntryPoints(Event ev)
    {
        var list = new List<string>();

        if (!string.IsNullOrWhiteSpace(ev.HangoutLink))
            list.Add(ev.HangoutLink);

        if (ev.ConferenceData?.EntryPoints != null)
        {
            foreach (var ep in ev.ConferenceData.EntryPoints)
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(ep.EntryPointType)) parts.Add(ep.EntryPointType);
                if (!string.IsNullOrWhiteSpace(ep.Uri)) parts.Add(ep.Uri);
                if (!string.IsNullOrWhiteSpace(ep.Label)) parts.Add(ep.Label);
                if (!string.IsNullOrWhiteSpace(ep.Pin)) parts.Add($"PIN:{ep.Pin}");
                var s = string.Join(" | ", parts);
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }
        }

        return list;
    }

    private static bool HasMeetRecording(Event ev)
    {
        if (ev.Attachments == null || ev.Attachments.Count == 0) return false;

        var titleEvent = (ev.Summary ?? "").Trim();

        foreach (var att in ev.Attachments)
        {
            var title = (att.Title ?? "").Trim();
            var mt = att.MimeType ?? "";

            bool isVideo =
                mt.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                mt.Equals("application/vnd.google-apps.video", StringComparison.OrdinalIgnoreCase);

            bool nameMatchesEvent =
                !string.IsNullOrWhiteSpace(titleEvent) &&
                (title.Equals(titleEvent, StringComparison.OrdinalIgnoreCase)
                 || title.Contains(titleEvent, StringComparison.OrdinalIgnoreCase)
                 || titleEvent.Contains(title, StringComparison.OrdinalIgnoreCase));

            bool keywords =
                title.Contains("record", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("grava", StringComparison.OrdinalIgnoreCase) ||
                (title.Contains("meet", StringComparison.OrdinalIgnoreCase)
                 && (title.Contains("rec", StringComparison.OrdinalIgnoreCase) || title.Contains("grava", StringComparison.OrdinalIgnoreCase)));

            if (isVideo || nameMatchesEvent || keywords)
                return true;
        }
        return false;
    }

    private static string CompactExtendedProps(Event ev)
    {
        try
        {
            if (ev.ExtendedProperties == null) return "";
            var type = ev.ExtendedProperties.GetType();
            var privProp = type.GetProperty("Private__");
            var sharedProp = type.GetProperty("Shared__");
            var dict = new Dictionary<string, object?>();

            var priv = privProp?.GetValue(ev.ExtendedProperties) as IDictionary<string, string>;
            var shared = sharedProp?.GetValue(ev.ExtendedProperties) as IDictionary<string, string>;

            if (priv != null && priv.Count > 0) dict["private"] = priv;
            if (shared != null && shared.Count > 0) dict["shared"] = shared;

            return dict.Count > 0 ? JsonSerializer.Serialize(dict) : "";
        }
        catch { return ""; }
    }
}

