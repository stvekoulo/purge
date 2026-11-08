using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.Views;

public sealed partial class AboutView : UserControl
{
    private UpdateInfo? _lastUpdateInfo;

    public AboutView()
    {
        InitializeComponent();
        Loc.LanguageChanged += SyncTexts;
        Loaded += (_, _) => OnLoaded();
    }

    private void OnLoaded()
    {
        SyncTexts();
        if (UpdateService.CachedUpdate is { } cached && cached.IsNewer)
        {
            ApplyUpdateInfo(cached);
        }
    }

    private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdate.IsBusy = true;
        BtnCheckUpdate.IsEnabled = false;
        TxtUpdateStatus.Text = Loc.T("Vérification sur GitHub en cours...", "Checking GitHub for updates...");

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync(silent: false);

            if (!result.Success)
            {
                TxtUpdateStatus.Text = result.ErrorMessage
                    ?? Loc.T("Impossible de contacter GitHub.", "Unable to reach GitHub.");
                IconUpdateStatus.Glyph = "\uE7BA"; // Warning
                return;
            }

            if (result.UpdateAvailable && result.Info != null)
            {
                ApplyUpdateInfo(result.Info);
            }
            else
            {
                _lastUpdateInfo = null;
                BadgeUpdateState.Visibility = Visibility.Collapsed;
                BtnDownloadUpdate.Visibility = Visibility.Collapsed;
                IconUpdateStatus.Glyph = "\uE73E"; // Checkmark
                TxtUpdateStatus.Text = Loc.T(
                    $"Vous utilisez la dernière version (v{UpdateService.CurrentVersion}).",
                    $"You are using the latest version (v{UpdateService.CurrentVersion}).");
            }
        }
        finally
        {
            BtnCheckUpdate.IsBusy = false;
            BtnCheckUpdate.IsEnabled = true;
        }
    }

    private void ApplyUpdateInfo(UpdateInfo info)
    {
        _lastUpdateInfo = info;
        BadgeUpdateState.Visibility = Visibility.Visible;
        BtnDownloadUpdate.Visibility = Visibility.Visible;
        IconUpdateStatus.Glyph = "\uE896"; // Download icon
        BtnDownloadUpdate.Content = Loc.T($"Télécharger v{info.LatestVersion}", $"Download v{info.LatestVersion}");
        TxtUpdateStatus.Text = Loc.T(
            $"Une nouvelle version v{info.LatestVersion} est disponible sur GitHub !",
            $"A new version v{info.LatestVersion} is available on GitHub!");
    }

    private void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenDownloadOrRelease(_lastUpdateInfo);
    }

    private void SyncTexts()
    {
        TxtVersion.Text = Loc.T("Version 1.0.2 pour Windows", "Version 1.0.2 for Windows");
        TxtTagline.Text = Loc.T("Libérez votre PC. En toute sécurité.", "Declutter your PC. Safely.");
        TxtUpdateHeader.Text = Loc.T("Mises à jour du logiciel", "Software Updates");
        TxtBadgeUpdateState.Text = Loc.T("Nouveau", "New");
        BtnCheckUpdate.Content = Loc.T("Vérifier les mises à jour", "Check for updates");

        if (_lastUpdateInfo != null && _lastUpdateInfo.IsNewer)
        {
            ApplyUpdateInfo(_lastUpdateInfo);
        }
        else
        {
            TxtUpdateStatus.Text = Loc.T(
                "Recherchez si une nouvelle version est disponible sur GitHub.",
                "Check if a newer version is available on GitHub.");
        }

        TxtCard1Title.Text = Loc.T("Corbeille par défaut", "Recycle Bin by Default");
        TxtCard1Body.Text = Loc.T(
            "Les fichiers supprimés sont toujours envoyés dans la Corbeille de Windows et ne sont jamais effacés définitivement sans votre accord.",
            "Cleaned files are always moved to the Windows Recycle Bin first and never permanently erased behind your back.");
        TxtCard2Title.Text = Loc.T("Liste autorisée stricte (Allowlist)", "Strict Allowlist Policy");
        TxtCard2Body.Text = Loc.T(
            "Seuls les répertoires et caches répertoriés dans le catalogue officiel sont nettoyés automatiquement. Rien d'inconnu n'est touché.",
            "Only caches listed in the verified allowlist catalog are eligible for safe cleaning. Unknown paths are never touched.");
        TxtCard3Title.Text = Loc.T("Support multi-disques & Confidentialité totale", "Multi-Drive Support & 100% Offline Privacy");
        TxtCard3Body.Text = Loc.T(
            "Analyse tous vos disques locaux (C:, D:, E:...) entièrement hors-ligne. Aucune donnée ni télémétrie n'est envoyée sur Internet.",
            "Scans all your local drives (C:, D:, E:...) completely offline. Zero network calls or telemetry.");
    }
}
