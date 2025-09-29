using CalendarsExportCsv.Domain.Models;
using System.Text;

namespace CalendarsExportCsv.Utils;

public static class CsvWriter
{
    public static string ToCsv(IEnumerable<CsvRow> rows)
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

    public static string BuildCsvFileName(string? singleCalendar, DateTime startInclusive, DateTime endInclusive)
    {
        var range = $"{startInclusive:yyyyMMdd}-{endInclusive:yyyyMMdd}";
        if (!string.IsNullOrWhiteSpace(singleCalendar))
        {
            var safe = new string(singleCalendar.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-').ToArray());
            return $"eventos_{safe}_{range}.csv";
        }
        return $"eventos_agendas_{range}.csv";
    }

    private static string Csv(string? value)
    {
        if (value == null) value = "";
        var v = value.Replace("\"", "\"\"");
        return $"\"{v}\"";
    }
}
