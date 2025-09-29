namespace CalendarsExportCsv.Domain.Models;

public sealed class ParsedDescription
{
    public string ReservadoPor { get; set; } = "";
    public string Email { get; set; } = "";
    public List<string> Telefones { get; set; } = new();
    public string CNPJ { get; set; } = "";
    public List<string> CNPJs { get; set; } = new();
    public string ID { get; set; } = "";
    public string Etapa { get; set; } = "";
    public List<string> Etapas { get; set; } = new();
    public string UF { get; set; } = "";
}
