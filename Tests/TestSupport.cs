namespace PurgeWin.Utilities
{
    public static class Loc
    {
        public static bool IsEn { get; set; }
        public static string T(string fr, string en) => IsEn ? en : fr;
    }
}

namespace PurgeWin.Services
{
    public static class AppData
    {
        public static string Folder => Path.Combine(Path.GetTempPath(), "PurgeUpdateTests");
    }
}
