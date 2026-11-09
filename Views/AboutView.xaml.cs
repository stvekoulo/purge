using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Services;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UpdateService.Shared.StateChanged += OnUpdateChanged;
            Loc.LanguageChanged += SyncTexts;
            PurgeStore.Shared.PropertyChanged += OnStoreChanged;
            SyncTexts();
        };
        Unloaded += (_, _) =>
        {
            UpdateService.Shared.StateChanged -= OnUpdateChanged;
            Loc.LanguageChanged -= SyncTexts;
            PurgeStore.Shared.PropertyChanged -= OnStoreChanged;
        };
    }

    private void OnUpdateChanged(UpdateState state) => DispatcherQueue.TryEnqueue(RefreshUpdate);
    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurgeStore.IsCleaning)) RefreshUpdate();
    }

    private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e) => await UpdateService.Shared.CheckAsync();
    private async void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e) => await UpdatePresentation.ActAsync();
    private void BtnCancelUpdate_Click(object sender, RoutedEventArgs e) => UpdateService.Shared.CancelDownload();

    private void RefreshUpdate()
    {
        var state = UpdateService.Shared.State;
        var text = UpdatePresentation.From(state);
        TxtUpdateStatus.Text = text.Detail;
        TxtUpdateHeader.Text = text.Title;
        IconUpdateStatus.Glyph = text.Glyph;
        BadgeUpdateState.Visibility = state.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        TxtBadgeUpdateState.Text = state.Stage == UpdateStage.Ready ? Loc.T("Prêt", "Ready") : Loc.T("Nouveau", "New");
        BtnCheckUpdate.Content = state.Stage == UpdateStage.Checking
            ? Loc.T("Vérification…", "Checking…") : Loc.T("Vérifier les mises à jour", "Check for updates");
        BtnCheckUpdate.IsBusy = state.Stage == UpdateStage.Checking;
        BtnCheckUpdate.IsEnabled = !state.IsBusy;
        BtnCheckUpdate.Visibility = state.Stage is UpdateStage.Ready or UpdateStage.Installing
            ? Visibility.Collapsed : Visibility.Visible;
        BtnDownloadUpdate.Visibility = state.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        BtnDownloadUpdate.Content = text.Action;
        BtnDownloadUpdate.IsEnabled = UpdatePresentation.CanAct(state);
        BtnDownloadUpdate.IsBusy = state.IsBusy;
        BtnCancelUpdate.Visibility = state.Stage == UpdateStage.Downloading ? Visibility.Visible : Visibility.Collapsed;
        BtnCancelUpdate.Content = Loc.T("Annuler", "Cancel");
        AboutDownloadProgress.Visibility = state.Stage is UpdateStage.Downloading or UpdateStage.Verifying
            ? Visibility.Visible : Visibility.Collapsed;
        AboutDownloadProgress.IsIndeterminate = state.Stage == UpdateStage.Verifying;
        AboutDownloadProgress.Value = state.Percent;
    }

    private void SyncTexts()
    {
        TxtVersion.Text = Loc.T($"Version {UpdateService.CurrentVersion} pour Windows", $"Version {UpdateService.CurrentVersion} for Windows");
        TxtTagline.Text = Loc.T("Libérez votre PC. En toute sécurité.", "Declutter your PC. Safely.");
        TxtCard1Title.Text = Loc.T("Corbeille par défaut", "Recycle Bin by Default");
        TxtCard1Body.Text = Loc.T(
            "Les fichiers nettoyés sont envoyés dans la Corbeille de Windows pour vous permettre de les restaurer.",
            "Cleaned files are moved to the Windows Recycle Bin so you can restore them.");
        TxtCard2Title.Text = Loc.T("Liste autorisée stricte", "Strict Allowlist Policy");
        TxtCard2Body.Text = Loc.T(
            "Seuls les répertoires et caches répertoriés dans le catalogue sont éligibles au nettoyage sécurisé.",
            "Only folders and caches listed in the catalog are eligible for safe cleaning.");
        TxtCard3Title.Text = Loc.T("Multi-disques & confidentialité", "Multiple drives & privacy");
        TxtCard3Body.Text = Loc.T(
            "L'analyse de vos disques reste locale. Seules la recherche et le téléchargement des mises à jour utilisent Internet. Aucune télémétrie n'est envoyée.",
            "Disk analysis stays local. Only update checks and downloads use the Internet. No telemetry is sent.");
        RefreshUpdate();
    }
}
