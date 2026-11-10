using System.Security.Cryptography;
using PurgeWin.Models;

namespace PurgeWin.Services;

/// Purge for Windows:
/// 1) Scans local AI model stores (Ollama, Hugging Face Hub, LM Studio, GPT4All, Jan)
/// 2) Groups byte-identical files (size -> first/last 16 KB -> SHA-256) and suggests a keeper.
public static class DuplicateAndAiScanner
{
    public static List<LargeFileItem> ScanAiModels(ISet<string> excluded, CancellationToken token, PathScope? scope = null)
    {
        var list = new List<LargeFileItem>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var hfRoot = Environment.GetEnvironmentVariable("HF_HUB_CACHE")
            ?? Path.Combine(home, ".cache", "huggingface", "hub");
        if (Directory.Exists(hfRoot) && (scope == null || scope.Allows(hfRoot)))
        {
            foreach (var dir in SafeDirs(hfRoot, "models--*"))
            {
                token.ThrowIfCancellationRequested();
                if (excluded.Contains(dir) || scope?.CanDelete(dir) == false) continue;
                var (bytes, mod) = FolderSizing.Measure(dir, token);
                if (bytes < LargeFileScanner.MinimumBytes) continue;
                var modelName = Path.GetFileName(dir).Replace("models--", "").Replace("--", "/");
                list.Add(new LargeFileItem
                {
                    FullPath = dir, Name = modelName, FolderDisplay = "Hugging Face Hub",
                    Bytes = bytes, LastModified = mod, Category = LargeFileCategory.AiModel,
                    SpecialLabel = "Modèle Hugging Face", SpecialLabelEn = "Hugging Face model",
                    SpecialExplanation = "Poids de modèle IA local stocké dans le cache Hugging Face Hub.",
                    SpecialExplanationEn = "Local AI model weights stored in the Hugging Face Hub cache.",
                });
            }
        }

        var ollamaBlobs = Path.Combine(home, ".ollama", "models", "blobs");
        if (Directory.Exists(ollamaBlobs) && !excluded.Contains(ollamaBlobs) && scope?.CanDelete(ollamaBlobs) != false)
        {
            var (bytes, mod) = FolderSizing.Measure(ollamaBlobs, token);
            if (bytes >= LargeFileScanner.MinimumBytes)
            {
                list.Add(new LargeFileItem
                {
                    FullPath = ollamaBlobs, Name = "Blobs de modèles Ollama", NameEn = "Ollama model blobs",
                    FolderDisplay = "Ollama (~/.ollama/models)",
                    Bytes = bytes, LastModified = mod, Category = LargeFileCategory.AiModel,
                    SpecialLabel = "Store Ollama",
                    SpecialExplanation = "Poids de modèles téléchargés par Ollama.",
                    SpecialExplanationEn = "Model weights downloaded by Ollama.",
                });
            }
        }

        string[] weightStores =
        [
            Path.Combine(home, ".lmstudio", "models"),
            Path.Combine(home, ".cache", "lm-studio", "models"),
            Path.Combine(localApp, "nomic.ai", "GPT4All"),
            Path.Combine(home, "jan", "models"),
        ];

        foreach (var store in weightStores.Where(p => Directory.Exists(p) && scope?.Allows(p) != false))
        {
            try
            {
                foreach (var f in new DirectoryInfo(store).EnumerateFiles("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
                }))
                {
                    token.ThrowIfCancellationRequested();
                    if (f.Length < 50L * 1024 * 1024 || excluded.Contains(f.FullName) || scope?.CanDelete(f.FullName) == false) continue;
                    list.Add(new LargeFileItem
                    {
                        FullPath = f.FullName, Name = f.Name, FolderDisplay = $"Modèle IA ({Path.GetFileName(store)})",
                        FolderDisplayEn = $"AI model ({Path.GetFileName(store)})",
                        Bytes = f.Length, LastModified = f.LastWriteTime, Category = LargeFileCategory.AiModel,
                        SpecialLabel = "Modèle IA local", SpecialLabelEn = "Local AI model",
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }
        return list;
    }

    public static void MarkDuplicates(List<LargeFileItem> items, CancellationToken token)
    {
        var candidates = items.Where(i => File.Exists(i.FullPath) && i.Category != LargeFileCategory.AiModel)
            .GroupBy(i => i.Bytes)
            .Where(g => g.Count() > 1);

        var groupIndex = 1;
        foreach (var sizeGroup in candidates)
        {
            token.ThrowIfCancellationRequested();
            var byQuick = sizeGroup.GroupBy(i => QuickHash(i.FullPath)).Where(g => g.Key != "" && g.Count() > 1);
            foreach (var qGroup in byQuick)
            {
                var byFull = qGroup.GroupBy(i => FullHash(i.FullPath, token)).Where(g => g.Key != "" && g.Count() > 1);
                foreach (var dupSet in byFull)
                {
                    var groupId = $"dup-{groupIndex++}";
                    // Keep the oldest copy outside Downloads when possible
                    var ordered = dupSet
                        .OrderBy(i => i.FullPath.Contains("\\Downloads\\", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                        .ThenBy(i => i.LastModified)
                        .ToList();
                    for (var idx = 0; idx < ordered.Count; idx++)
                    {
                        ordered[idx].DuplicateGroupId = groupId;
                        ordered[idx].DuplicateGroupCount = ordered.Count;
                        ordered[idx].IsDuplicateKeeper = idx == 0;
                    }
                }
            }
        }
    }

    private static string QuickHash(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buf = new byte[16384];
            var read1 = fs.Read(buf, 0, 8192);
            if (fs.Length > 8192)
            {
                fs.Seek(-Math.Min(8192, fs.Length), SeekOrigin.End);
                read1 += fs.Read(buf, 8192, 8192);
            }
            return Convert.ToHexString(SHA256.HashData(buf.AsSpan(0, read1)));
        }
        catch { return ""; }
    }

    private static string FullHash(string path, CancellationToken token)
    {
        try
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buf = new byte[65536];
            int r;
            while ((r = fs.Read(buf, 0, buf.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                sha.AppendData(buf, 0, r);
            }
            return Convert.ToHexString(sha.GetHashAndReset());
        }
        catch { return ""; }
    }

    private static IEnumerable<string> SafeDirs(string root, string pattern)
    {
        try { return Directory.EnumerateDirectories(root, pattern).ToList(); }
        catch { return []; }
    }
}
