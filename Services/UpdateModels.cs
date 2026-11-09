namespace PurgeWin.Services;

public enum UpdateStage { Idle, Checking, UpToDate, Available, Downloading, Verifying, Ready, Installing, Error }
public enum UpdateError { Network, Release, NoInstaller, Integrity, Download, Install }

public sealed record UpdateAsset(string Name, string Url, long Size, string Sha256);
public sealed record UpdateInfo(string LatestVersion, string ReleaseTitle, string ReleaseNotes,
    string ReleaseUrl, UpdateAsset? Asset);

public sealed record UpdateState(UpdateStage Stage = UpdateStage.Idle, UpdateInfo? Info = null,
    long DownloadedBytes = 0, long TotalBytes = 0, string? InstallerPath = null,
    UpdateError? Error = null)
{
    public bool IsBusy => Stage is UpdateStage.Checking or UpdateStage.Downloading
        or UpdateStage.Verifying or UpdateStage.Installing;
    public bool HasUpdate => Info != null && Stage != UpdateStage.UpToDate;
    public double Percent => TotalBytes > 0 ? Math.Clamp(DownloadedBytes * 100d / TotalBytes, 0, 100) : 0;
}

public sealed class UpdateException(UpdateError error) : Exception(error.ToString())
{
    public UpdateError Error { get; } = error;
}
