using PurgeWin.Services;

internal static class ScopeTests
{
    public static async Task Run(Action<string, bool> check)
    {
        var scope = new PathScope([@"E:\"], [@"E:\Projects\Keep"]);
        check("Selected disk accepts its own files", scope.Allows(@"e:\Projects\Other\cache"));
        check("Selected disk rejects another disk", !scope.Allows(@"C:\Users\me\cache"));
        check("Empty scope never means every disk", !new PathScope([]).Allows(@"C:\anything"));
        check("Exclusions include descendants", !scope.Allows(@"E:\Projects\Keep\node_modules"));
        check("Excluded siblings remain visible", scope.Allows(@"E:\Projects\Keeper\cache"));
        check("Deleting a parent cannot erase an exclusion", !scope.CanDelete(@"E:\Projects"));
        check("Canonical paths cannot escape scope", !scope.Allows(@"C:\Else\..\cache"));
        check("Path prefixes respect directory boundaries", !PathScope.IsWithin(@"E:\Projects2", @"E:\Projects"));
        check("Relative and shell paths are never deletion targets", !scope.CanDelete("shell:RecycleBinFolder") && !scope.CanDelete("cache"));
        check("All disks still respect exclusions", new PathScope([@"C:\", @"E:\"], [@"E:\Keep"]).Allows(@"C:\Temp\cache")
            && !new PathScope([@"C:\", @"E:\"], [@"E:\Keep"]).Allows(@"E:\Keep\cache"));
        using var session = new ScopeSession();
        var c = session.Change("C:\\", new PathScope([@"C:\"]));
        var e = session.Change("E:\\", scope);
        check("Disk change cancels previous analysis", c.Token.IsCancellationRequested);
        check("Only the current disk can publish results", !session.IsCurrent(c) && session.IsCurrent(e));
        var backToC = session.Change("C:\\", new PathScope([@"C:\"]));
        check("Returning to same disk cannot revive old results", !session.IsCurrent(c) && session.IsCurrent(backToC));
        var deferred = Task.Run(async () => { await Task.Delay(20); return session.IsCurrent(backToC); });
        session.Change("ALL", new PathScope([@"C:\", @"E:\"]));
        check("Late completion cannot replace new disk results", !await deferred);
        session.Cancel();
        check("Cancelled scans cannot publish", !session.IsCurrent(session.Current));
    }
}
