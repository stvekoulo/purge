using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PurgeWin.Services;

internal sealed class UpdateScenarios
{
    public readonly byte[] Payload = Enumerable.Range(0, 131072).Select(i => (byte)(i % 251)).ToArray();
    public string Folder { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N"));
    public string Digest => Convert.ToHexString(SHA256.HashData(Payload));
    public int Downloads;
    public int Launches;
    public bool Offline;
    public bool Corrupt;
    public bool Slow;
    public string Version = "1.0.4";
    public bool PreRelease;
    public bool MissingAsset;
    public string? LastDirectory;
    public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string Release(string architecture = "x64", string? digest = null, string? url = null) => JsonSerializer.Serialize(new
    {
        tag_name = "v" + Version,
        name = "Purge " + Version,
        body = "Update fixture",
        prerelease = PreRelease,
        draft = false,
        assets = MissingAsset ? [] : new[] { new {
            name = $"Purge-{Version}-win-{architecture}-Setup.exe",
            size = Payload.Length,
            browser_download_url = url ?? $"https://github.com/stvekoulo/purge/releases/download/v{Version}/Purge-{Version}-win-{architecture}-Setup.exe",
            digest = "sha256:" + (digest ?? Digest)
        } }
    });

    public UpdateCoordinator Create(Action<string, string, int>? start = null, string current = "1.0.3")
    {
        var http = new HttpClient(new Handler(async (request, token) =>
        {
            if (Offline) throw new HttpRequestException("Offline fixture");
            if (request.RequestUri!.Host == "api.github.com")
                return new(HttpStatusCode.OK) { Content = new StringContent(Release(), Encoding.UTF8, "application/json") };
            Interlocked.Increment(ref Downloads);
            await Task.Yield();
            return new(HttpStatusCode.OK) { Content = Slow
                ? new StreamContent(new SlowStream(Payload, Started))
                : new ByteArrayContent(Corrupt ? new byte[Payload.Length] : Payload) };
        }));
        return new(http, Folder, current, "x64", start ?? ((path, dir, pid) => { Launches++; LastDirectory = dir; }));
    }

    public void Cleanup()
    {
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => callback(request, token);
    }

    private sealed class SlowStream(byte[] bytes, TaskCompletionSource started) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            started.TrySetResult();
            await Task.Delay(30, token);
            return await base.ReadAsync(buffer[..Math.Min(4096, buffer.Length)], token);
        }
    }
}
