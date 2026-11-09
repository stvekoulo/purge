using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

internal static class FolderAndTrashTests
{
    public static async Task Run(Action<string, bool> check)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Visible"));
        Directory.CreateDirectory(Path.Combine(root, "Keep"));
        Directory.CreateDirectory(Path.Combine(root, "Deep", "a", "b", "c"));
        await File.WriteAllBytesAsync(Path.Combine(root, "Visible", "test.bin"), new byte[100]);
        await File.WriteAllBytesAsync(Path.Combine(root, "Keep", "excluded.bin"), new byte[100]);
        await File.WriteAllBytesAsync(Path.Combine(root, "Deep", "a", "b", "c", "test.bin"), new byte[100]);
        try
        {
            var scope = new PathScope([root], [Path.Combine(root, "Keep")]);
            var folders = await FolderExplorer.ReadAsync(root, scope, default);
            check("Explorer omits excluded folders", folders.All(i => i.Name != "Keep"));
            check("Explorer measures visible files", folders.Single(i => i.Name == "Visible").Bytes == 100);
            check("Ordinary file ancestors remain eligible", !PathExpander.HasReparsePointAncestor(Path.Combine(root, "Visible", "test.bin")));
            check("Deep samples are explicitly estimates", folders.Single(i => i.Name == "Deep").IsEstimate);
            var files = await FolderExplorer.ReadAsync(Path.Combine(root, "Visible"), scope, default);
            check("Folder navigation exposes exact file sizes", files.Single().Bytes == 100 && !files.Single().IsDirectory && !files.Single().IsEstimate);
            check("Explorer cannot leave selected scope", (await FolderExplorer.ReadAsync(Path.GetPathRoot(root)!, scope, default)).Count == 0);
            var map = await EverythingElseScanner.MeasureAsync(1000, 9999999, 0, [root], excluded: scope.Excluded);
            check("Map leaves unmeasured space unattributed", map.Any(i => i.Bucket == EverythingElseBucket.Unmeasured && i.FullPath == ""));
            check("Map never fabricates Windows or runtime sizes", map.All(i => i.Id is not ("win:os" or "win:programfiles")));
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            try { await FolderExplorer.ReadAsync(root, scope, cancel.Token); check("Explorer honours cancellation", false); }
            catch (OperationCanceledException) { check("Explorer honours cancellation", true); }
        }
        finally { Directory.Delete(root, true); }

        var bulk = new BulkObservableCollection<int>();
        int events = 0;
        bulk.CollectionChanged += (_, _) => events++;
        bulk.ReplaceAll(Enumerable.Range(0, 1000));
        check("Large result sets produce one UI reset", events == 1 && bulk.Count == 1000);
        List<string> emptyRequests = [];
        var emptied = RecycleBin.EmptyCore([@"E:\", @"E:\", @"C:\forbidden", ""], _ => 100, path => { emptyRequests.Add(path); return 0; });
        check("Emptying targets only requested drive roots", emptied.Requested == 1 && emptyRequests.SequenceEqual([@"E:\"]));
        check("No roots can never empty all disks", RecycleBin.EmptyCore([], _ => 100, _ => throw new Exception()).Requested == 0);
        check("Empty bins are skipped", RecycleBin.EmptyCore([@"E:\"], _ => 0, _ => throw new Exception()).Requested == 0);
        check("Native empty failures are reported", RecycleBin.EmptyCore([@"E:\"], _ => 100, _ => unchecked((int)0x80070005)).Failures.Count == 1);
    }
}
