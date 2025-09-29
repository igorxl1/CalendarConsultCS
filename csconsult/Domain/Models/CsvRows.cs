namespace CalendarsExportCsv.Domain.Models;

public sealed class CsvRow
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
