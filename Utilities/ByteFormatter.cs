using System.Globalization;

namespace PurgeWin.Utilities;

/// Purge for Windows: decimal storage units ("GB" in English, "Go" in French).
public static class ByteFormatter
{
    private static readonly string[] UnitsFr = ["octets", "Ko", "Mo", "Go", "To"];
    private static readonly string[] UnitsEn = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes)
    {
        bool en = Loc.IsEn;
        var units = en ? UnitsEn : UnitsFr;
        if (bytes <= 0) return en ? "0 KB" : "0 Ko";
        double value = bytes;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        if (unit == 0) return $"{bytes} {units[0]}";
        var format = value >= 100 || unit == 1 ? "0" : "0.0";
        var culture = en ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("fr-FR");
        return $"{value.ToString(format, culture)} {units[unit]}";
    }

    public static string Percent(long part, long total)
    {
        if (total <= 0) return Loc.IsEn ? "0%" : "0 %";
        var pct = (double)part / total * 100;
        return Loc.IsEn
            ? $"{pct.ToString("0.0", CultureInfo.InvariantCulture)}%"
            : $"{pct.ToString("0.0", CultureInfo.GetCultureInfo("fr-FR"))} %";
    }
}
