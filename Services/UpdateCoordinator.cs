namespace PurgeWin.Services;

public sealed class UpdateCoordinator(HttpClient http, string folder, string currentVersion, string architecture,
    Action<string, string, int>? startInstaller = null)
{
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly UpdateDownloader _downloader = new(http, folder);
    private CancellationTokenSource? _downloadCts;
    private UpdateState _state = new();
    public UpdateState State => Volatile.Read(ref _state);
    public event Action<UpdateState>? StateChanged;

    private void Set(UpdateState state)
    {
        Volatile.Write(ref _state, state);
        StateChanged?.Invoke(state);
    }

    public async Task CheckAsync()
    {
        if (!await _operation.WaitAsync(0)) return;
        var previous = State;
        try
        {
            if (previous.Stage is UpdateStage.Ready or UpdateStage.Installing) return;
            Set(new(UpdateStage.Checking));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var json = await http.GetStringAsync(UpdateRelease.Endpoint, timeout.Token);
            var info = UpdateRelease.Parse(json, currentVersion, architecture);
            if (info == null) { Set(new(UpdateStage.UpToDate)); return; }
            if (info.Asset == null) { Set(new(UpdateStage.Error, info, Error: UpdateError.NoInstaller)); return; }
            string path = _downloader.GetPath(info.Asset);
            Set(await _downloader.IsVerifiedAsync(path, info.Asset, timeout.Token)
                ? new(UpdateStage.Ready, info, info.Asset.Size, info.Asset.Size, path)
                : new(UpdateStage.Available, info));
        }
        catch (UpdateException e) { Set(new(UpdateStage.Error, Error: e.Error)); }
        catch (System.Text.Json.JsonException) { Set(new(UpdateStage.Error, Error: UpdateError.Release)); }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            Set(new(UpdateStage.Error, previous.Info, Error: UpdateError.Network));
        }
        catch (Exception) { Set(new(UpdateStage.Error, Error: UpdateError.Release)); }
        finally { _operation.Release(); }
    }

    public async Task DownloadAsync()
    {
        if (!await _operation.WaitAsync(0)) return;
        var info = State.Info;
        try
        {
            if (info?.Asset == null || State.Stage is UpdateStage.Ready or UpdateStage.Installing) return;
            using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            _downloadCts = cancel;
            Set(new(UpdateStage.Downloading, info, TotalBytes: info.Asset.Size));
            string path = await _downloader.DownloadAsync(info.Asset,
                bytes => Set(new(UpdateStage.Downloading, info, bytes, info.Asset.Size)),
                () => Set(new(UpdateStage.Verifying, info, info.Asset.Size, info.Asset.Size)), cancel.Token);
            Set(new(UpdateStage.Ready, info, info.Asset.Size, info.Asset.Size, path));
        }
        catch (OperationCanceledException) { Set(new(UpdateStage.Available, info)); }
        catch (UpdateException e) { Set(new(UpdateStage.Error, info, Error: e.Error)); }
        catch (Exception) { Set(new(UpdateStage.Error, info, Error: UpdateError.Download)); }
        finally { _downloadCts = null; _operation.Release(); }
    }

    public void CancelDownload()
    {
        try { _downloadCts?.Cancel(); }
        catch (ObjectDisposedException) { /* A completed download cannot be cancelled. */ }
    }

    public async Task<bool> RestartToInstallAsync(string installationFolder, int processId, Func<bool>? canRestart = null)
    {
        if (!await _operation.WaitAsync(0)) return false;
        var state = State;
        try
        {
            if (state.Stage != UpdateStage.Ready || state.Info?.Asset == null || state.InstallerPath == null) return false;
            Set(state with { Stage = UpdateStage.Installing });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            if (!await _downloader.IsVerifiedAsync(state.InstallerPath, state.Info.Asset, timeout.Token))
                throw new UpdateException(UpdateError.Integrity);
            if (canRestart?.Invoke() == false) { Set(state); return false; }
            (startInstaller ?? UpdateInstaller.Start)(state.InstallerPath, installationFolder, processId);
            return true;
        }
        catch (UpdateException e) { Set(state with { Stage = UpdateStage.Error, Error = e.Error }); }
        catch (Exception) { Set(state with { Stage = UpdateStage.Ready, Error = UpdateError.Install }); }
        finally { _operation.Release(); }
        return false;
    }
}
