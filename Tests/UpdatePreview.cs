#if PURGE_UPDATE_PREVIEW
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace PurgeWin.Services;

// Included only with /p:UpdatePreview=true. No executable can be launched.
internal static class UpdatePreview
{
    private static readonly byte[] Payload = new byte[8 * 1024 * 1024];
    public static UpdateCoordinator Create() => new(new HttpClient(new Handler()),
        Path.Combine(AppContext.BaseDirectory, "preview-download"), UpdateService.CurrentVersion,
        "x64", (_, _, _) => throw new InvalidOperationException("Preview cannot install software."));

    private sealed class Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.Host == "api.github.com")
            {
                var release = JsonSerializer.Serialize(new
                {
                    tag_name = "v1.0.5", prerelease = false, draft = false,
                    assets = new[] { new {
                        name = "Purge-1.0.5-win-x64-Setup.exe", size = Payload.Length,
                        digest = "sha256:" + Convert.ToHexString(SHA256.HashData(Payload)),
                        browser_download_url = "https://github.com/stvekoulo/purge/releases/download/v1.0.5/Purge-1.0.5-win-x64-Setup.exe"
                    } }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(release) });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new PreviewStream(Payload)) });
        }
    }

    private sealed class PreviewStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            await Task.Delay(120, token);
            return await base.ReadAsync(buffer[..Math.Min(32768, buffer.Length)], token);
        }
    }
}
#endif
