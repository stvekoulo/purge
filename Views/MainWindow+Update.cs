using Microsoft.UI.Xaml;
using PurgeWin.Services;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class MainWindow
{
    private string? _dismissedUpdate;

    private void InitializeUpdateDetection()
    {
        UpdateService.Shared.StateChanged += OnUpdateStateChanged;
        Loc.LanguageChanged += RefreshUpdateUI;
        Closed += (_, _) =>
        {
            UpdateService.Shared.StateChanged -= OnUpdateStateChanged;
            Loc.LanguageChanged -= RefreshUpdateUI;
        };
        RefreshUpdateUI();
        if (PurgeStore.Shared.Settings.AutoCheckUpdates) _ = CheckUpdatesAfterLaunchAsync();
    }

    private static async Task CheckUpdatesAfterLaunchAsync()
    {
        await Task.Delay(2500);
        await UpdateService.Shared.CheckAsync();
    }

    private void OnUpdateStateChanged(UpdateState state) => DispatcherQueue.TryEnqueue(RefreshUpdateUI);

    private void RefreshUpdateUI()
    {
        var state = UpdateService.Shared.State;
        var text = UpdatePresentation.From(state);
        bool show = state.HasUpdate && (_dismissedUpdate != state.Info?.LatestVersion
            || state.Stage is UpdateStage.Downloading or UpdateStage.Verifying or UpdateStage.Installing);
        BadgeNavAboutUpdate.Visibility = state.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        UpdateBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        TxtUpdateBannerTitle.Text = text.Title;
        TxtUpdateBannerDesc.Text = text.Detail;
        IconUpdateBanner.Glyph = text.Glyph;
        BtnBannerDownload.Content = text.Action;
        BtnBannerDownload.IsEnabled = UpdatePresentation.CanAct(state);
        BtnBannerDownload.IsBusy = state.IsBusy;
        BtnBannerCancel.Visibility = state.Stage == UpdateStage.Downloading ? Visibility.Visible : Visibility.Collapsed;
        BtnBannerCancel.Content = Loc.T("Annuler", "Cancel");
        BtnBannerDismiss.Visibility = state.IsBusy ? Visibility.Collapsed : Visibility.Visible;
        UpdateDownloadProgress.Visibility = state.Stage is UpdateStage.Downloading or UpdateStage.Verifying
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateDownloadProgress.IsIndeterminate = state.Stage == UpdateStage.Verifying;
        UpdateDownloadProgress.Value = state.Percent;
        Microsoft.UI.Xaml.Controls.ToolTipService.SetToolTip(BtnBannerDismiss, Loc.T("Masquer la notification", "Dismiss notification"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(BtnBannerDismiss, Loc.T("Masquer la notification de mise à jour", "Dismiss update notification"));
    }

    private async void BtnBannerDownload_Click(object sender, RoutedEventArgs e) => await UpdatePresentation.ActAsync();
    private void BtnBannerCancel_Click(object sender, RoutedEventArgs e) => UpdateService.Shared.CancelDownload();

    private void BtnBannerDismiss_Click(object sender, RoutedEventArgs e)
    {
        _dismissedUpdate = UpdateService.Shared.State.Info?.LatestVersion;
        RefreshUpdateUI();
    }
}
