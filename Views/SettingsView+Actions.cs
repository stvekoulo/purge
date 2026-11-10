using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class SettingsView
{
    private async Task ShowIntegrationFailureAsync(string message)
    {
        if (XamlRoot is null) return;
        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Loc.T("Action impossible", "Action failed"),
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
        }.ShowAsync();
    }

    private void CmbStale_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CmbStale is null) return;
        PurgeStore.Shared.Settings.StaleProjectDays = CmbStale.SelectedIndex switch { 1 => 30, 2 => 90, 3 => 180, _ => 0 };
        PurgeStore.Shared.SaveSettings();
        PurgeStore.Shared.InvalidateAnalysis();
    }

    private void OnAutoUpdateToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || TglAutoUpdate is null) return;
        PurgeStore.Shared.Settings.AutoCheckUpdates = TglAutoUpdate.IsOn;
        PurgeStore.Shared.SaveSettings();
    }

    private async void OnAutoEmptyChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || ChkAutoEmptyTrash is null) return;
        bool enable = ChkAutoEmptyTrash.IsChecked == true;
        if (enable && !PurgeStore.Shared.AutoEmptyRecycleBin)
        {
            ChkAutoEmptyTrash.IsEnabled = false;
            try
            {
                if (XamlRoot is null) enable = false;
                else
                {
                    var confirmation = new ContentDialog
                    {
                        XamlRoot = XamlRoot,
                        Title = Loc.T("Vider automatiquement toute la Corbeille ?", "Automatically empty the entire Recycle Bin?"),
                        Content = new TextBlock
                        {
                            Text = Loc.T("Après chaque nettoyage, les éléments déjà présents dans la Corbeille des disques concernés seront aussi supprimés définitivement. Cette opération ne peut pas être annulée.",
                                "After each cleanup, older items already in the Recycle Bin on the affected drives will also be permanently deleted. This cannot be undone."),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        PrimaryButtonText = Loc.T("Activer", "Enable"),
                        CloseButtonText = Loc.T("Annuler", "Cancel"),
                        DefaultButton = ContentDialogButton.Close,
                    };
                    enable = await confirmation.ShowAsync() == ContentDialogResult.Primary;
                }
            }
            finally { ChkAutoEmptyTrash.IsEnabled = true; }
        }
        PurgeStore.Shared.SetAutoEmptyRecycleBin(enable);
        if (ChkAutoEmptyTrash.IsChecked != enable)
        {
            _loading = true;
            ChkAutoEmptyTrash.IsChecked = enable;
            _loading = false;
        }
    }

    private async void OnAddProjectFolderClick(object sender, RoutedEventArgs e)
    {
        if (App.CurrentWindow is null) return;
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null && !PurgeStore.Shared.Settings.CustomProjectRoots.Contains(folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            PurgeStore.Shared.Settings.CustomProjectRoots.Add(folder.Path);
            PurgeStore.Shared.SaveSettings();
            PurgeStore.Shared.InvalidateAnalysis();
            RefreshFolderList(CustomRootsPanel, PurgeStore.Shared.Settings.CustomProjectRoots, OnRemoveCustomRootClick);
        }
    }

    private void OnRemoveCustomRootClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            PurgeStore.Shared.Settings.CustomProjectRoots.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            PurgeStore.Shared.SaveSettings();
            PurgeStore.Shared.InvalidateAnalysis();
            RefreshFolderList(CustomRootsPanel, PurgeStore.Shared.Settings.CustomProjectRoots, OnRemoveCustomRootClick);
        }
    }

    private void OnRemoveExclusionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            PurgeStore.Shared.Settings.ExcludedPaths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            PurgeStore.Shared.SaveSettings();
            PurgeStore.Shared.InvalidateAnalysis();
            RefreshExclusions();
        }
    }

    private void OnReplayOnboardingClick(object sender, RoutedEventArgs e) =>
        ReplayOnboardingRequested?.Invoke();

    private async void OnSelfUninstallClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ThemeBrushes.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            Title = Loc.T("Désinstaller Purge ?", "Uninstall Purge?"),
            Content = Loc.T(
                "Cela supprimera le démarrage automatique, la tâche planifiée et enverra les données locales (%LOCALAPPDATA%\\Purge) dans la Corbeille.",
                "This removes the startup entry, scheduled task, and moves local app data (%LOCALAPPDATA%\\Purge) to the Recycle Bin."),
            PrimaryButtonText = Loc.T("Désinstaller et quitter", "Uninstall & Quit"),
            CloseButtonText = Loc.T("Annuler", "Cancel"),
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            SystemIntegration.UninstallPurge(removeAppData: true);
            App.CurrentWindow?.Close();
        }
    }
}
