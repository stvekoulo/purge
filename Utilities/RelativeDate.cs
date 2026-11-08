namespace PurgeWin.Utilities;

/// Port of RelativeDateText (bilingual FR/EN).
public static class RelativeDate
{
    public static string Format(DateTime date)
    {
        var span = DateTime.Now - date;
        if (span.TotalMinutes < 1) return Loc.T("à l'instant", "just now");
        if (span.TotalMinutes < 60)
        {
            var m = (int)span.TotalMinutes;
            return Loc.T($"il y a {m} min", $"{m}m ago");
        }
        if (span.TotalHours < 24)
        {
            var h = (int)span.TotalHours;
            return Loc.T($"il y a {h} h", $"{h}h ago");
        }
        if (span.TotalDays < 30)
        {
            var d = (int)span.TotalDays;
            return Loc.T($"il y a {d} j", $"{d}d ago");
        }
        return Loc.IsEn ? $"on {date:MMM d, yyyy}" : $"le {date:d MMM yyyy}";
    }
}
