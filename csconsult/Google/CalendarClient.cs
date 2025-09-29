using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;

namespace CalendarsExportCsv.Google;

public sealed class CalendarClient
{
    private readonly CalendarService _service;
    private readonly string _tz;

    public CalendarClient(CalendarService service, string timeZoneId)
    {
        _service = service;
        _tz = timeZoneId;
    }

    public async Task<IList<Event>> FetchEventsAsync(string calendarId, DateTime startLocal, DateTime endExclusiveLocal)
    {
        var req = _service.Events.List(calendarId);

        req.TimeMinDateTimeOffset = new DateTimeOffset(startLocal);
        req.TimeMaxDateTimeOffset = new DateTimeOffset(endExclusiveLocal);
        req.TimeZone = _tz;
        req.ShowDeleted = false;
        req.SingleEvents = true;
        req.ShowHiddenInvitations = true;
        req.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        req.MaxResults = 2500;

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

        async Task FetchAllAsync()
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

        try
        {
            await FetchAllAsync();
        }
        catch (global::Google.GoogleApiException ex) when ((int)ex.HttpStatusCode == 400
               && ex.Message.Contains("Invalid field selection", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("⚠️ Campos parciais não suportados. Repetindo sem 'Fields'…");
            req.Fields = null;
            req.PageToken = null;
            all.Clear();
            await FetchAllAsync();
        }


        return all;
    }
}
