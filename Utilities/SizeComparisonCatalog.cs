using System.Globalization;

namespace PurgeWin.Utilities;

/// Purge for Windows:
/// translates byte counts into relatable comparisons in FR and EN.
public static class SizeComparisonCatalog
{
    private const long Mb = 1024L * 1024;
    private const long Gb = 1024L * 1024 * 1024;

    private sealed record Anchor(
        long Bytes,
        string Glyph,
        bool IsTimesOver,
        string OneFr, string ManyFr,
        string OneEn, string ManyEn);

    private static readonly Anchor[] Anchors =
    [
        new(2 * Mb, "\uE722", false,
            "une capture d'écran de plus", "captures d'écran de plus",
            "one more screenshot", "more screenshots"),
        new(500 * Mb, "\uE8F2", true,
            "l'application Slack", "Slack",
            "Slack", "Slack"),
        new((12 * Gb) / 10, "\uE774", false,
            "une copie de Chrome", "copies de Chrome",
            "a copy of Chrome", "copies of Chrome"),
        new(5 * Gb, "\uE790", true,
            "Photoshop", "Photoshop",
            "Photoshop", "Photoshop"),
        new(12 * Gb, "\uE770", false,
            "la prochaine mise à jour Windows", "mises à jour majeures Windows",
            "the next Windows feature update", "Windows feature updates"),
        new(15 * Gb, "\uE943", false,
            "une installation complète de Visual Studio", "installations de Visual Studio",
            "a full Visual Studio install", "copies of Visual Studio"),
        new(105 * Gb, "\uE7FC", true,
            "tout GTA V", "GTA V",
            "all of GTA V", "GTA V"),
        new(256 * Gb, "\uE7F8", false,
            "un SSD complet de 256 Go", "SSD complets de 256 Go",
            "a full 256 GB SSD", "full 256 GB SSDs"),
    ];

    public static (string Glyph, string Label)? Describe(long bytes)
    {
        if (bytes < 5 * Mb) return null;

        var candidates = Anchors
            .Select(a => (Anchor: a, Mult: (double)bytes / a.Bytes))
            .Where(x => x.Mult >= 1.0 && x.Mult <= 20_000.0)
            .Select(x => (x.Anchor, x.Mult, Score: Math.Abs(Math.Log10(x.Mult) - Math.Log10(2.0))))
            .OrderBy(x => x.Score)
            .ToList();

        if (candidates.Count == 0) return null;
        var best = candidates[0];
        var bestFits = candidates.Where(x => x.Score <= best.Score + 0.3).Take(3).ToList();
        var pick = bestFits[StableIndex(bytes, bestFits.Count)];

        var count = Math.Max(1, (int)Math.Floor(pick.Mult));
        var countText = count.ToString("N0", Loc.IsEn ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("fr-FR"));
        var a = pick.Anchor;
        string label;
        if (!a.IsTimesOver)
        {
            label = count == 1
                ? Loc.T($"De la place pour {a.OneFr}", $"Room for {a.OneEn}")
                : Loc.T($"De la place pour {countText} {a.ManyFr}", $"Room for {countText} {a.ManyEn}");
        }
        else
        {
            label = count == 1
                ? Loc.T($"De la place pour {a.OneFr}", $"Room for {a.OneEn}")
                : count == 2
                    ? Loc.T($"De la place pour {a.ManyFr}, deux fois", $"Room for {a.ManyEn}, twice over")
                    : Loc.T($"De la place pour {a.ManyFr}, {countText} fois", $"Room for {a.ManyEn}, {countText} times over");
        }
        return (a.Glyph, label);
    }

    public static string TimeTagline()
    {
        var hour = DateTime.Now.Hour;
        return hour switch
        {
            >= 5 and < 12 => Loc.T("Un PC propre pour bien commencer la matinée.", "A clean PC to start the morning."),
            >= 12 and < 18 => Loc.T("Plus d'espace libre pour votre après-midi.", "More breathing room for your afternoon."),
            >= 18 and < 22 => Loc.T("Un disque léger pour terminer la journée.", "A lighter drive to wrap up the day."),
            _ => Loc.T("Nettoyage nocturne terminé en douceur.", "Late-night cleanup, neatly done."),
        };
    }

    private static int StableIndex(long bytes, int count)
    {
        ulong mixed = unchecked((ulong)bytes * 2_654_435_761UL);
        return (int)((mixed >> 32) % (ulong)Math.Max(1, count));
    }
}
