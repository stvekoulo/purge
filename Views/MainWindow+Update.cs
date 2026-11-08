using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using PurgeWin.Services;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class MainWindow
{
    private UpdateInfo? _activeUpdateInfo;

    private void InitializeUpdateDetection()
    {
        UpdateService.UpdateAvailable += OnUpdateDiscovered;

        // Si une mise à jour a déjà été mise en cache
        if (UpdateService.CachedUpdate is { } cached && cached.IsNewer)
        {
            ShowUpdateBanner(cached);
        }

        // Détection automatique en tâche de fond au démarrage (si activée)
        if (PurgeStore.Shared.Settings.AutoCheckUpdates)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500); // Ne ralentit aucunement l'affichage initial
                await UpdateService.CheckForUpdatesAsync(silent: true);
            });
        }
    }

    private void OnUpdateDiscovered(UpdateInfo info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ShowUpdateBanner(info);
        });
    }

    private void ShowUpdateBanner(UpdateInfo info)
    {
        _activeUpdateInfo = info;
        BadgeNavAboutUpdate.Visibility = Visibility.Visible;
        UpdateBanner.Visibility = Visibility.Visible;

        TxtUpdateBannerTitle.Text = Loc.T(
            $"Mise à jour v{info.LatestVersion} disponible",
            $"Update v{info.LatestVersion} available");

        TxtUpdateBannerDesc.Text = Loc.T(
            "Une nouvelle version de Purge pour Windows est prête au téléchargement.",
            "A newer release of Purge for Windows is ready to download.");

        BtnBannerDownload.Content = Loc.T("Télécharger", "Download");
    }

    private void BtnBannerDownload_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenDownloadOrRelease(_activeUpdateInfo);
    }

    private void BtnBannerDismiss_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
    }
}
