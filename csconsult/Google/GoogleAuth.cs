using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace CalendarsExportCsv.Google;

public static class GoogleAuth
{
    public static async Task<CalendarService> CreateCalendarServiceAsync()
    {
        using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);

        var tokenRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalendarsExportCsv", "tokens");
        Directory.CreateDirectory(tokenRoot);

        var creds = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { CalendarService.Scope.CalendarReadonly },
            "user",
            CancellationToken.None,
            new FileDataStore(tokenRoot, true));

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = creds,
            ApplicationName = "CalendarsExportCsv"
        });
    }
}
