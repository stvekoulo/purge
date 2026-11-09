using PurgeWin.Services;

if (args.Contains("--live-download"))
{
    var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("Purge-Update-Validation/1.0");
    var live = new UpdateCoordinator(http, Path.GetFullPath(args[1]), "0.0.0", "x64",
        (_, _, _) => throw new InvalidOperationException("Live validation never installs software."));
    await live.CheckAsync();
    Console.WriteLine($"Public release check: {live.State.Stage} ({live.State.Info?.LatestVersion})");
    await live.DownloadAsync();
    Console.WriteLine($"Download and SHA-256 validation: {live.State.Stage}, {live.State.DownloadedBytes} bytes");
    return live.State.Stage == UpdateStage.Ready ? 0 : 1;
}

if (args.Contains("--installer-probe"))
{
    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "restarted.txt"), DateTime.UtcNow.ToString("O"));
    return 0;
}
if (args.Contains("--wait-for-update"))
{
    await Task.Delay(5000);
    return 0;
}

int failed = 0;
void Check(string name, bool result)
{
    Console.WriteLine($"{(result ? "PASS" : "FAIL")}: {name}");
    if (!result) failed++;
}
Check("Version comes from the running assembly", UpdateService.CurrentVersion == "1.0.3");
Check("Three and four part versions are equal", UpdateService.CompareVersions("1.0.3", "1.0.3.0") == 0);
Check("Same version is never an update", UpdateService.CompareVersions("1.0.3", "1.0.3") == 0);
Check("New patch is an update", UpdateService.CompareVersions("1.0.4", "1.0.3") > 0);
Check("Version prefixes and metadata are normalized", UpdateService.CompareVersions("v1.0.3+build", "1.0.3.0") == 0);
var fixture = new UpdateScenarios();
Check("Current release is not offered", UpdateRelease.Parse(fixture.Release(), "1.0.4", "x64") == null);
Check("Older release is not offered", UpdateRelease.Parse(fixture.Release(), "1.1.0", "x64") == null);
Check("Correct architecture is selected", UpdateRelease.Parse(fixture.Release(), "1.0.3", "x64")?.Asset?.Name.EndsWith("x64-Setup.exe") == true);
Check("Missing architecture does not offer another installer", UpdateRelease.Parse(fixture.Release(), "1.0.3", "arm64")?.Asset == null);
fixture.PreRelease = true;
Check("Prereleases are not offered", UpdateRelease.Parse(fixture.Release(), "1.0.3", "x64") == null);
fixture.PreRelease = false;
try { UpdateRelease.Parse(fixture.Release(url: "https://evil.example/Setup.exe"), "1.0.3", "x64"); Check("Untrusted URLs rejected", false); }
catch (UpdateException) { Check("Untrusted URLs rejected", true); }
try { UpdateRelease.Parse(fixture.Release(digest: "invalid"), "1.0.3", "x64"); Check("Missing checksum rejected", false); }
catch (UpdateException) { Check("Missing checksum rejected", true); }
try { UpdateService.CompareVersions("garbage", "1.0.3"); Check("Invalid version cannot trigger a download", false); }
catch (UpdateException) { Check("Invalid version cannot trigger a download", true); }

try
{
    var updater = fixture.Create();
    List<UpdateStage> stages = [];
    updater.StateChanged += state => stages.Add(state.Stage);
    await updater.CheckAsync();
    Check("New release becomes available", updater.State.Stage == UpdateStage.Available);
    await Task.WhenAll(updater.DownloadAsync(), updater.DownloadAsync(), updater.CheckAsync());
    Check("Concurrent clicks produce one download", fixture.Downloads == 1);
    Check("Download passes through verification to ready", stages.Contains(UpdateStage.Downloading)
        && stages.Contains(UpdateStage.Verifying) && updater.State.Stage == UpdateStage.Ready);
    Check("Ready file has expected bytes", File.ReadAllBytes(updater.State.InstallerPath!).SequenceEqual(fixture.Payload));
    Check("Progress reaches the full size", updater.State.DownloadedBytes == fixture.Payload.Length && updater.State.Percent == 100);
    var restored = fixture.Create();
    await restored.CheckAsync();
    Check("Verified download survives application restart", restored.State.Stage == UpdateStage.Ready);
    await restored.DownloadAsync();
    Check("Ready update is not downloaded twice", fixture.Downloads == 1);
    Check("Cleaning blocks install handoff", !await restored.RestartToInstallAsync(@"C:\Purge Test", 123, () => false)
        && restored.State.Stage == UpdateStage.Ready && fixture.Launches == 0);
    Check("Restart hands off only on explicit action", await restored.RestartToInstallAsync(@"C:\Purge Test", 123)
        && fixture.Launches == 1 && fixture.LastDirectory == @"C:\Purge Test");

    var cancelledUac = fixture.Create((_, _, _) => throw new System.ComponentModel.Win32Exception(1223));
    await cancelledUac.CheckAsync();
    Check("Cancelled installer keeps app ready", !await cancelledUac.RestartToInstallAsync(@"C:\Purge Test", 123)
        && cancelledUac.State.Stage == UpdateStage.Ready && cancelledUac.State.Error == UpdateError.Install);

    var tampered = fixture.Create();
    await tampered.CheckAsync();
    File.WriteAllBytes(tampered.State.InstallerPath!, [0]);
    Check("Tampering after download blocks execution", !await tampered.RestartToInstallAsync(@"C:\Purge Test", 123)
        && tampered.State.Error == UpdateError.Integrity && fixture.Launches == 1);
    fixture.Corrupt = true;
    var corrupt = fixture.Create();
    await corrupt.CheckAsync();
    await corrupt.DownloadAsync();
    Check("Corrupt download never becomes ready", corrupt.State.Error == UpdateError.Integrity);
    Check("Failed partial files are removed", !Directory.GetFiles(fixture.Folder, "*.partial-*").Any());
    fixture.Corrupt = false;
    await corrupt.DownloadAsync();
    Check("Retry recovers after corruption", corrupt.State.Stage == UpdateStage.Ready);
}
finally { fixture.Cleanup(); }

var cancelFixture = new UpdateScenarios { Slow = true };
try
{
    var updater = cancelFixture.Create();
    await updater.CheckAsync();
    var downloading = updater.DownloadAsync();
    await cancelFixture.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    updater.CancelDownload();
    await downloading;
    Check("Cancellation returns to available", updater.State.Stage == UpdateStage.Available);
    Check("Cancelled partial file is removed", !Directory.GetFiles(cancelFixture.Folder, "*.partial-*").Any());
    cancelFixture.Slow = false;
    await updater.DownloadAsync();
    Check("Cancelled download can be retried", updater.State.Stage == UpdateStage.Ready);
}
finally { cancelFixture.Cleanup(); }

var offlineFixture = new UpdateScenarios { Offline = true };
var offline = offlineFixture.Create();
await offline.CheckAsync();
Check("Offline check is recoverable", offline.State.Error == UpdateError.Network);
offlineFixture.Offline = false;
await offline.CheckAsync();
Check("Network recovery allows checking again", offline.State.Stage == UpdateStage.Available);
offlineFixture.MissingAsset = true;
await offline.CheckAsync();
Check("Missing installer is explicit", offline.State.Error == UpdateError.NoInstaller);
offlineFixture.Version = "1.0.3";
await offline.CheckAsync();
Check("A subsequent current version clears stale badge", offline.State.Stage == UpdateStage.UpToDate && offline.State.Info == null);

var plan = UpdateInstaller.BuildStartInfo(@"C:\Downloads\Purge Setup.exe", @"C:\Program Files\Purge", 123, true);
Check("Installer targets existing folder with correct scope", plan.ArgumentList.Contains(@"/DIR=C:\Program Files\Purge") && plan.ArgumentList.Contains("/ALLUSERS"));
Check("Installer waits for parent and requests relaunch", plan.ArgumentList.Contains("/PURGEPID=123") && plan.ArgumentList.Contains("/PURGEUPDATE=1"));
Check("Update never reboots Windows or force-closes applications", plan.ArgumentList.Contains("/NORESTART") && plan.ArgumentList.Contains("/NOCLOSEAPPLICATIONS"));
Console.WriteLine($"Failures: {failed}");
return failed == 0 ? 0 : 1;
