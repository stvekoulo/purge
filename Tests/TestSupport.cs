namespace PurgeWin.Utilities
{
    public static class Loc
    {
        public static string T(string fr, string en) => fr;
    }
}

namespace PurgeWin.Services
{
    public static class AppData
    {
        public static string Folder => Path.Combine(Path.GetTempPath(), "PurgeUpdateTests");
    }
}
