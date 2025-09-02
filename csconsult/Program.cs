using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

class Program
{
    const string CalendarsListFile = "calendars.txt"; // para consultas em massa

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("pt-BR");

        // args: [0]=email(opc), [1]=inicio yyyy-MM-dd(opc), [2]=fim yyyy-MM-dd(opc)
        string? singleCalendar = (args.Length >= 1 && args[0].Contains('@')) ? args[0].Trim() : null;
        var (timeMin, timeMax) = GetRange(args, singleCalendar != null ? 1 : 0);

        var service = await CreateCalendarServiceAsync();

        List<string> calendarIds;
        if (!string.IsNullOrWhiteSpace(singleCalendar))
        {
            calendarIds = new List<string> { singleCalendar };
            Console.WriteLine($"📅 Modo isolado: {singleCalendar}");
        }
        else
        {
            calendarIds = LoadCalendars(CalendarsListFile);
            if (calendarIds.Count == 0)
            {
                Console.WriteLine($"⚠️ {CalendarsListFile} não encontrado ou vazio.");
                Console.Write("Digite um e-mail de agenda para consulta isolada: ");
                var typed = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(typed) || !typed.Contains('@'))
                {
                    Console.WriteLine("❌ Nenhuma agenda válida informada.");
                    return;
                }
                calendarIds = new List<string> { typed! };
                Console.WriteLine($"📅 Modo isolado: {typed}");
            }
            else
            {
                Console.WriteLine($"📚 Modo em massa: {calendarIds.Count} agendas lidas de {CalendarsListFile}");
            }
        }

        Console.WriteLine($"🔎 Intervalo: {timeMin:yyyy-MM-dd} → {timeMax:yyyy-MM-dd}");

        var rows = new List<CsvRow>();

        foreach (var calId in calendarIds)
        {
            try
            {
                Console.WriteLine($"\n➡️  Consultando: {calId}");
                var events = await FetchEventsAsync(service, calId, timeMin, timeMax);

                foreach (var ev in events)
                {
                    var descPlain = StripHtml(ev.Description ?? "");
                    var parsed = ParseDescription(descPlain);

                    var attendees = ev.Attendees?.Select(a =>
                        string.IsNullOrWhiteSpace(a.DisplayName) ? a.Email : $"{a.DisplayName} <{a.Email}>")
                        .ToList() ?? new List<string>();

                    var confPoints = BuildConferenceEntryPoints(ev);
                    var attachments = ev.Attachments?.Select(att =>
                        $"{att.Title} ({att.MimeType}) -> {att.FileUrl ?? att.IconLink ?? ""}")
                        .ToList() ?? new List<string>();

                    var extProps = CompactExtendedProps(ev);

                    rows.Add(new CsvRow
                    {
                        Agenda = calId,
                        EventId = ev.Id ?? "",
                        Titulo = ev.Summary ?? "",
                        Local = ev.Location ?? "",
                        HtmlLink = ev.HtmlLink ?? "",
                        DataInicio = ev.Start?.DateTime?.ToString("yyyy-MM-dd HH:mm") ?? ev.Start?.Date,
                        DataFim = ev.End?.DateTime?.ToString("yyyy-MM-dd HH:mm") ?? ev.End?.Date,
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

                        // CNPJ (primeiro) e CNPJs (todos)
                        CNPJ = parsed.CNPJs.FirstOrDefault() ?? "",
                        CNPJs = string.Join(" | ", parsed.CNPJs),

                        ID = parsed.ID,

                        // Etapa (primeira) e Etapas (todas)
                        Etapa = parsed.Etapa,
                        Etapas = string.Join(" | ", parsed.Etapas),

                        UF = parsed.UF,

                        Descricao = descPlain.Replace("\r", " ").Replace("\n", " ").Trim()
                    });
                }

                Console.WriteLine($"   ✅ {rows.Count(r => r.Agenda == calId)} eventos acumulados dessa agenda.");
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                Console.WriteLine($"   ⛔ Sem permissão para ler {calId}. Compartilhe a agenda com “Ver todos os detalhes do evento”.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Erro ao ler {calId}: {ex.Message}");
            }
        }

        var csvPath = BuildCsvFileName(singleCalendar, timeMin, timeMax);
        File.WriteAllText(csvPath, ToCsv(rows), Encoding.UTF8);
        Console.WriteLine($"\n✅ CSV gerado: {csvPath}");
    }

    // ---------- Auth ----------
    static async Task<CalendarService> CreateCalendarServiceAsync()
    {
        using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);
        var credPath = "token_multi_calendars";
        var creds = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { CalendarService.Scope.CalendarReadonly },
            "user",
            CancellationToken.None,
            new FileDataStore(credPath, true));

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = creds,
            ApplicationName = "CalendarsExportCsv"
        });
    }

    // ---------- Events.List com retry ----------
    static async Task<IList<Event>> FetchEventsAsync(CalendarService service, string calendarId, DateTime timeMin, DateTime timeMax)
    {
        var req = service.Events.List(calendarId);
        req.TimeMin = timeMin;
        req.TimeMax = timeMax;
        req.ShowDeleted = false;
        req.SingleEvents = true;            // expande recorrentes
        req.ShowHiddenInvitations = true;   // inclui convites ocultos
        req.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        req.MaxResults = 2500;

        // Partial response “seguro”; se der 400 (Invalid field selection), refaz sem Fields
        req.Fields =
            "items(" +
                "id,summary,description,location,htmlLink,hangoutLink," +
                "conferenceData(entryPoints,conferenceSolution(name,iconUri),notes,conferenceId,signature)," +
                "creator(displayName,email)," +
                "organizer(displayName,email)," +
                "attendees(displayName,email,responseStatus,optional,resource,organizer)," +
                "start(date,dateTime,timeZone)," +
                "end(date,dateTime,timeZone)," +
                "attachments(fileId,fileUrl,title,mimeType,iconLink)," +
                "extendedProperties," +
                "recurrence,recurringEventId,visibility,transparency,updated" +
            "),nextPageToken";

        var all = new List<Event>();

        async Task fetchPageAsync()
        {
            var resp = await req.ExecuteAsync();
            if (resp.Items != null) all.AddRange(resp.Items);
            while (!string.IsNullOrEmpty(resp.NextPageToken))
            {
                req.PageToken = resp.NextPageToken;
                resp = await req.ExecuteAsync();
                if (resp.Items != null) all.AddRange(resp.Items);
            }
        }

        try { await fetchPageAsync(); }
        catch (Google.GoogleApiException ex) when ((int)ex.HttpStatusCode == 400 && ex.Message.Contains("Invalid field selection", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("⚠️ Campos parciais não suportados nesta conta/versão. Repetindo sem 'Fields'…");
            req.Fields = null;
            req.PageToken = null;
            all.Clear();
            await fetchPageAsync();
        }

        return all;
    }

    // ---------- Datas ----------
    static (DateTime timeMin, DateTime timeMax) GetRange(string[] args, int offset)
    {
        DateTime start, end;
        if (args.Length >= offset + 2
            && DateTime.TryParse(args[offset + 0], out start)
            && DateTime.TryParse(args[offset + 1], out end))
        {
            return (start, end);
        }
        start = DateTime.Today;
        end = DateTime.Today.AddDays(30);
        return (start, end);
    }

    // ---------- Carrega e-mails (massa) ----------
    static List<string> LoadCalendars(string path)
    {
        if (!File.Exists(path)) return new List<string>();
        return File.ReadAllLines(path)
                   .Select(l => l.Trim())
                   .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList();
    }

    // ---------- Helpers de texto ----------
    static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var text = Regex.Replace(html, "(?i)<br\\s*/?>", "\n");
        text = Regex.Replace(text, "<.*?>", " ");
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }

    static string MatchAfterLabel(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.Multiline);
        if (m.Success && m.Groups.Count > 1) return m.Groups[1].Value.Trim();
        return "";
    }

    static string NormalizePhone(string raw)
    {
        var digits = DigitsOnly(raw);
        if (digits.Length == 11) return $"({digits[..2]}) {digits[2]}{digits[3..7]}-{digits[7..]}";
        if (digits.Length == 10) return $"({digits[..2]}) {digits[2..6]}-{digits[6..]}";
        return digits;
    }
    static string DigitsOnly(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    // ---------- Parser da descrição ----------
    static ParsedDescription ParseDescription(string text)
    {
        var p = new ParsedDescription();

        p.ReservadoPor = MatchAfterLabel(text, @"(?im)^\s*Reservado por\s*:\s*(.+)$");

        var email = Regex.Match(text, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");
        p.Email = email.Success ? email.Value : "";

        var tels = Regex.Matches(text, @"(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}");
        p.Telefones = tels.Select(m => NormalizePhone(m.Value)).Distinct().ToList();

        // CNPJs: todos que aparecerem
        var cnpjMatches = Regex.Matches(text, @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b");
        p.CNPJs = cnpjMatches.Select(m => DigitsOnly(m.Value))
                             .Where(d => d.Length == 14)
                             .Distinct()
                             .ToList();
        p.CNPJ = p.CNPJs.FirstOrDefault() ?? ""; // compatibilidade

        // ID
        var id = MatchAfterLabel(text, @"(?im)^\s*ID\s*:\s*([A-Za-z0-9\-_.]+)$");
        if (string.IsNullOrEmpty(id))
        {
            var m = Regex.Match(text, @"(?im)^\s*ID\s*[:\-]?\s*([A-Za-z0-9\-_.]+)\s*$");
            id = m.Success ? m.Groups[1].Value.Trim() : "";
        }
        p.ID = id;

        // ETAPAS (todas) + Etapa (primeira)
        var etapas = ExtractEtapas(text);
        p.Etapas = etapas;
        p.Etapa = etapas.FirstOrDefault() ?? "";

        // UF
        var uf = MatchAfterLabel(text, @"(?im)^\s*UF\s*:\s*([A-Za-z]{2})\s*$");
        p.UF = uf?.ToUpperInvariant() ?? "";

        return p;
    }

    // Extrai etapas em vários formatos
    static List<string> ExtractEtapas(string text)
    {
        var etapas = new List<string>();

        // A) "Etapa: valor" ou "Etapa - valor"
        foreach (Match m in Regex.Matches(text, @"(?im)^\s*Etapa\s*[:\-]\s*(.+)$"))
            etapas.Add(m.Groups[1].Value.Trim());

        // B) "Etapa" em uma linha e o valor na próxima linha não vazia
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            if (Regex.IsMatch(lines[i], @"(?i)^\s*Etapa\s*$"))
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[j]))
                    {
                        etapas.Add(lines[j].Trim());
                        break;
                    }
                }
            }
        }

        // C) formatos soltos: "2ª Etapa", "2 Etapa", "2a Etapa"
        foreach (Match m in Regex.Matches(text, @"(?i)\b(\d+\s*(?:ª|a)?\s*etapa)\b"))
            etapas.Add(m.Groups[1].Value.Trim());

        // dedup case-insensitive
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var e in etapas)
        {
            if (string.IsNullOrWhiteSpace(e)) continue;
            if (seen.Add(e)) result.Add(e);
        }
        return result;
    }

    // ---------- Gravação do Meet ----------
    static bool HasMeetRecording(Event ev)
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
                (
                    title.Equals(titleEvent, StringComparison.OrdinalIgnoreCase) ||
                    title.Contains(titleEvent, StringComparison.OrdinalIgnoreCase) ||
                    titleEvent.Contains(title, StringComparison.OrdinalIgnoreCase)
                );

            bool keywords =
                title.Contains("record", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("grava", StringComparison.OrdinalIgnoreCase) ||
                (title.Contains("meet", StringComparison.OrdinalIgnoreCase) &&
                 (title.Contains("rec", StringComparison.OrdinalIgnoreCase) || title.Contains("grava", StringComparison.OrdinalIgnoreCase)));

            if (isVideo || nameMatchesEvent || keywords)
                return true;
        }
        return false;
    }

    // ---------- Conference entry points ----------
    static List<string> BuildConferenceEntryPoints(Event ev)
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

    // ---------- ExtendedProperties via reflexão ----------
    static string CompactExtendedProps(Event ev)
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

    // ---------- CSV ----------
    static string ToCsv(IEnumerable<CsvRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";",
            Csv("Agenda"), Csv("EventId"), Csv("Título"), Csv("Local"), Csv("HtmlLink"),
            Csv("Início"), Csv("Fim"), Csv("Timezone"),
            Csv("Meet"), Csv("ConferenceEntryPoints"),
            Csv("Organizador"),
            Csv("CriadorNome"), Csv("CriadorEmail"),
            Csv("Convidados"),
            Csv("Evento ocorrido"),
            Csv("Anexos"), Csv("ExtendedProperties"),
            Csv("ReservadoPor"), Csv("Email"), Csv("Telefones"),
            Csv("CNPJ"), Csv("CNPJs"),
            Csv("ID"), Csv("Etapa"), Csv("Etapas"), Csv("UF"),
            Csv("Descrição")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(";",
                Csv(r.Agenda), Csv(r.EventId), Csv(r.Titulo), Csv(r.Local), Csv(r.HtmlLink),
                Csv(r.DataInicio), Csv(r.DataFim), Csv(r.Timezone),
                Csv(r.LinkMeet), Csv(r.ConferenceEntryPoints),
                Csv(r.Organizador),
                Csv(r.CriadorNome), Csv(r.CriadorEmail),
                Csv(r.Convidados),
                Csv(r.EventoOcorrido),
                Csv(r.Anexos), Csv(r.ExtendedProperties),
                Csv(r.ReservadoPor), Csv(r.EmailNaDescricao), Csv(r.Telefones),
                Csv(r.CNPJ), Csv(r.CNPJs),
                Csv(r.ID), Csv(r.Etapa), Csv(r.Etapas), Csv(r.UF),
                Csv(r.Descricao)));
        }
        return sb.ToString();
    }

    static string Csv(string? value)
    {
        if (value == null) value = "";
        var v = value.Replace("\"", "\"\"");
        return $"\"{v}\"";
    }

    static string BuildCsvFileName(string? singleCalendar, DateTime start, DateTime end)
    {
        var range = $"{start:yyyyMMdd}-{end:yyyyMMdd}";
        if (!string.IsNullOrWhiteSpace(singleCalendar))
        {
            var safe = new string(singleCalendar.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray());
            return $"eventos_{safe}_{range}.csv";
        }
        return $"eventos_agendas_{range}.csv";
    }

    // ---------- modelos ----------
    class ParsedDescription
    {
        public string ReservadoPor { get; set; } = "";
        public string Email { get; set; } = "";
        public List<string> Telefones { get; set; } = new();
        public string CNPJ { get; set; } = "";           // primeiro (compat)
        public List<string> CNPJs { get; set; } = new(); // todos
        public string ID { get; set; } = "";
        public string Etapa { get; set; } = "";          // primeira ocorrência
        public List<string> Etapas { get; set; } = new();// todas as ocorrências
        public string UF { get; set; } = "";
    }

    class CsvRow
    {
        public string Agenda { get; set; } = "";
        public string EventId { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Local { get; set; } = "";
        public string HtmlLink { get; set; } = "";
        public string? DataInicio { get; set; }
        public string? DataFim { get; set; }
        public string Timezone { get; set; } = "";
        public string LinkMeet { get; set; } = "";
        public string ConferenceEntryPoints { get; set; } = "";
        public string Organizador { get; set; } = "";
        public string CriadorNome { get; set; } = "";
        public string CriadorEmail { get; set; } = "";
        public string Convidados { get; set; } = "";
        public string EventoOcorrido { get; set; } = "Não";
        public string Anexos { get; set; } = "";
        public string ExtendedProperties { get; set; } = "";
        public string ReservadoPor { get; set; } = "";
        public string EmailNaDescricao { get; set; } = "";
        public string Telefones { get; set; } = "";
        public string CNPJ { get; set; } = "";   // primeiro
        public string CNPJs { get; set; } = "";  // todos
        public string ID { get; set; } = "";
        public string Etapa { get; set; } = "";  // primeira
        public string Etapas { get; set; } = ""; // todas (separadas por " | ")
        public string UF { get; set; } = "";
        public string Descricao { get; set; } = "";
    }
}
