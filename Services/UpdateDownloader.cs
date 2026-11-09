using System.Diagnostics;
using System.Security.Cryptography;

namespace PurgeWin.Services;

public sealed class UpdateDownloader(HttpClient http, string folder)
{
    public async Task<bool> IsVerifiedAsync(string path, UpdateAsset asset, CancellationToken token)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != asset.Size) return false;
        await using var file = File.OpenRead(path);
        var digest = await SHA256.HashDataAsync(file, token);
        return Convert.ToHexString(digest).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public string GetPath(UpdateAsset asset) => Path.Combine(folder, Path.GetFileName(asset.Name));

    public async Task<string> DownloadAsync(UpdateAsset asset, Action<long> progress,
        Action verifying, CancellationToken token)
    {
        if (!UpdateRelease.IsTrustedUrl(asset.Url) || Path.GetFileName(asset.Name) != asset.Name)
            throw new UpdateException(UpdateError.Integrity);
        Directory.CreateDirectory(folder);
        var path = GetPath(asset);
        if (await IsVerifiedAsync(path, asset, token)) return path;
        var partial = path + ".partial-" + Guid.NewGuid().ToString("N");
        try
        {
            using var response = await http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != asset.Size)
                throw new UpdateException(UpdateError.Integrity);
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                var timer = Stopwatch.StartNew();
                int read;
                while ((read = await input.ReadAsync(buffer, token)) != 0)
                {
                    total += read;
                    if (total > asset.Size) throw new UpdateException(UpdateError.Integrity);
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    if (timer.ElapsedMilliseconds >= 100) { progress(total); timer.Restart(); }
                }
                progress(total);
            }
            verifying();
            if (!await IsVerifiedAsync(partial, asset, token)) throw new UpdateException(UpdateError.Integrity);
            File.Move(partial, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
