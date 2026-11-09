using System.Runtime.InteropServices;

namespace PurgeWin.Services;

public sealed record TrashActionResult(int Requested, IReadOnlyList<string> Failures);

public static partial class RecycleBin
{
    [DllImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr owner, string rootPath, uint flags);

    public static Task<TrashActionResult> EmptyAsync(IReadOnlyList<string> roots) => Task.Run(() =>
        EmptyCore(roots, Count, root => SHEmptyRecycleBin(IntPtr.Zero, root, 1 | 2 | 4)));

    internal static TrashActionResult EmptyCore(IEnumerable<string> roots, Func<string, long> size, Func<string, int> empty)
    {
        var failures = new List<string>();
        int requested = 0;
        foreach (var root in ValidRoots(roots))
        {
            if (size(root) == 0) continue;
            int result = empty(root);
            if (result < 0) failures.Add($"{root} (0x{result:X8})");
            else requested++;
        }
        return new TrashActionResult(requested, failures);
    }

    public static Task<TrashActionResult> RestoreAsync(IReadOnlyList<string> roots, IReadOnlyList<string>? originalPaths = null)
    {
        var completion = new TaskCompletionSource<TrashActionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope = new PathScope(ValidRoots(roots));
        var thread = new Thread(() =>
        {
            object? shell = null, bin = null, items = null;
            var failures = new List<string>();
            int requested = 0;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", throwOnError: true)!);
                bin = ((dynamic)shell!).NameSpace(10);
                items = ((dynamic)bin!).Items();
                // Iterate backwards: restoration removes items from the Shell collection.
                for (int i = (int)((dynamic)items).Count - 1; i >= 0; i--)
                {
                    object? entry = null;
                    try
                    {
                        entry = ((dynamic)items).Item(i);
                        string path = ((dynamic)entry!).Path;
                        if (!scope.Contains(path)) continue;
                        if (originalPaths != null)
                        {
                            string parent = ((dynamic)entry).ExtendedProperty("System.Recycle.DeletedFrom") as string ?? "";
                            string name = ((dynamic)entry).Name;
                            string original = Path.Combine(parent, name);
                            if (!originalPaths.Any(p => PathScope.Normalize(p)?.Equals(PathScope.Normalize(original), StringComparison.OrdinalIgnoreCase) == true)) continue;
                        }
                        ((dynamic)entry).InvokeVerbEx("undelete", "");
                        requested++;
                    }
                    catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                    { failures.Add(ex.Message); }
                    finally { Release(entry); }
                }
                completion.TrySetResult(new(requested, failures));
            }
            catch (Exception ex) { completion.TrySetResult(new(requested, [ex.Message])); }
            finally { Release(items); Release(bin); Release(shell); }
        }) { IsBackground = true, Name = "Purge-RecycleBin-Restore" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    // Only drive roots are valid. Never pass null (which would target all drives).
    public static IReadOnlyList<string> ValidRoots(IEnumerable<string> roots) => roots
        .Select(PathScope.Normalize).Where(p => p != null && p.Equals(Path.GetPathRoot(p), StringComparison.OrdinalIgnoreCase))
        .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
