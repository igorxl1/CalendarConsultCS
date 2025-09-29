namespace CalendarsExportCsv.Domain;

public static class DateRange
{
    /// <summary>Resolve (start, endExclusive). Sem datas: hoje → +30 dias. Uma data: só o dia. Duas datas: inclui o último dia.</summary>
    public static (DateTime startLocal, DateTime endExclusiveLocal) Resolve(DateTime? startArg, DateTime? endArg)
    {
        if (startArg is null)
        {
            var today = DateTime.Today;
            return (DateTime.SpecifyKind(today, DateTimeKind.Local),
                    DateTime.SpecifyKind(today.AddDays(30), DateTimeKind.Local));
        }

        var start = DateTime.SpecifyKind(startArg.Value.Date, DateTimeKind.Local);

        if (endArg is null)
            return (start, start.AddDays(1));

        var end = DateTime.SpecifyKind(endArg.Value.Date, DateTimeKind.Local);
        if (end < start) end = start;
        return (start, end.AddDays(1));
    }
}
