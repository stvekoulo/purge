using System.Globalization;

namespace PurgeWin.Utilities;

/// Bilingual (FR / EN) helper for Purge for Windows.
public static class Loc
{
    private static string _lang = DetectDefault();

    public static event Action? LanguageChanged;

    public static string CurrentLanguage => _lang;
    public static bool IsEn => string.Equals(_lang, "en", StringComparison.OrdinalIgnoreCase);

    public static void SetLanguage(string? code)
    {
        var normalized = string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : string.Equals(code, "fr", StringComparison.OrdinalIgnoreCase)
                ? "fr"
                : DetectDefault();

        if (_lang == normalized) return;
        _lang = normalized;
        LanguageChanged?.Invoke();
    }

    public static string T(string fr, string en) => IsEn ? en : fr;

    private static string DetectDefault()
    {
        var twoLetter = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return string.Equals(twoLetter, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "fr";
    }
}
