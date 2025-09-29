using CalendarsExportCsv.Domain.Models;
using CalendarsExportCsv.Utils;
using System.Text.RegularExpressions;

namespace CalendarsExportCsv.Parsing;

public static class DescriptionParser
{
    public static ParsedDescription Parse(string text)
    {
        var p = new ParsedDescription();

        p.ReservadoPor = MatchAfterLabel(text, @"(?im)^\s*Reservado por\s*:\s*(.+)$");

        var email = Regex.Match(text, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}");
        p.Email = email.Success ? email.Value : "";

        var tels = Regex.Matches(text, @"(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?\d{4,5}[-\s]?\d{4}");
        p.Telefones = tels.Select(m => TextUtils.NormalizePhone(m.Value)).Distinct().ToList();

        var cnpjMatches = Regex.Matches(text, @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b");
        p.CNPJs = cnpjMatches.Select(m => TextUtils.DigitsOnly(m.Value))
                             .Where(d => d.Length == 14)
                             .Distinct()
                             .ToList();
        p.CNPJ = p.CNPJs.FirstOrDefault() ?? "";

        var id = MatchAfterLabel(text, @"(?im)^\s*ID\s*:\s*([A-Za-z0-9\-_.]+)$");
        if (string.IsNullOrEmpty(id))
        {
            var m = Regex.Match(text, @"(?im)^\s*ID\s*[:\-]?\s*([A-Za-z0-9\-_.]+)\s*$");
            id = m.Success ? m.Groups[1].Value.Trim() : "";
        }
        p.ID = id;

        var etapas = ExtractEtapas(text);
        p.Etapas = etapas;
        p.Etapa = etapas.FirstOrDefault() ?? "";

        var uf = MatchAfterLabel(text, @"(?im)^\s*UF\s*:\s*([A-Za-z]{2})\s*$");
        p.UF = uf?.ToUpperInvariant() ?? "";

        return p;
    }

    private static string MatchAfterLabel(string text, string pattern)
    {
        var m = Regex.Match(text, pattern, RegexOptions.Multiline);
        if (m.Success && m.Groups.Count > 1) return m.Groups[1].Value.Trim();
        return "";
    }

    private static List<string> ExtractEtapas(string text)
    {
        var etapas = new List<string>();

        foreach (Match m in Regex.Matches(text, @"(?im)^\s*Etapa\s*[:\-]\s*(.+)$"))
            etapas.Add(m.Groups[1].Value.Trim());

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

        foreach (Match m in Regex.Matches(text, @"(?i)\b(\d+\s*(?:ª|a)?\s*etapa)\b"))
            etapas.Add(m.Groups[1].Value.Trim());

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var e in etapas)
        {
            if (string.IsNullOrWhiteSpace(e)) continue;
            if (seen.Add(e)) result.Add(e);
        }
        return result;
    }
}
