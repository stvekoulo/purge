using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

/// <summary>
/// Installed applications must be removed by their registered uninstaller.
/// Their installation folders and user data are never deleted as a shortcut.
/// </summary>
public static class UninstallReviewDialog
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, InstalledAppItem app, PurgeStore store)
    {
        if (xamlRoot is null) return false;
        var canLaunch = app.CanUninstall && !string.IsNullOrWhiteSpace(app.UninstallString);
        var panel = new StackPanel { Spacing = 12, MinWidth = 400, MaxWidth = 520 };

        var heading = new StackPanel { Spacing = 3 };
        heading.Children.Add(new TextBlock
        {
            Text = app.DisplayName, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(app.Publisher))
            heading.Children.Add(new TextBlock { Text = app.Publisher, FontSize = 12, Foreground = ThemeBrushes.Get("TextSecondary") });
        panel.Children.Add(heading);

        panel.Children.Add(new TextBlock
        {
            Text = canLaunch
                ? Loc.T("Purge lancera le désinstalleur officiel de cette application. Suivez ses instructions pour retirer correctement les composants, services et entrées système.",
                    "Purge will launch this application's registered uninstaller. Follow its instructions to remove components, services and system entries correctly.")
                : Loc.T("Aucun désinstalleur utilisable n'a été trouvé dans ce périmètre. Sélectionnez le disque concerné ou désinstallez l'application dans les Paramètres Windows.",
                    "No usable uninstaller was found in this scope. Select the installation drive or uninstall the app through Windows Settings."),
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = ThemeBrushes.Get("TextSecondary")
        });

        if (app.Leftovers.Count > 0)
        {
            panel.Children.Add(new Border
            {
                Background = ThemeBrushes.Get("StatusCheckFill"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
                Child = new TextBlock
                {
                    Text = Loc.T(
                        $"{app.Leftovers.Count} dossier(s) de données associés ont été détectés ({ByteFormatter.Format(app.LeftoverBytes)}). Ils ne seront pas effacés pendant la désinstallation. Relancez l'analyse ensuite pour vérifier les éventuels restes.",
                        $"{app.Leftovers.Count} associated data folder(s) were found ({ByteFormatter.Format(app.LeftoverBytes)}). They will not be erased during uninstallation. Scan again afterwards to review possible leftovers."),
                    FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = ThemeBrushes.Get("StatusCheckText")
                }
            });
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ThemeBrushes.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            Title = Loc.T($"Désinstaller {app.DisplayName} ?", $"Uninstall {app.DisplayName}?"),
            Content = panel,
            PrimaryButtonText = Loc.T("Lancer le désinstalleur", "Launch uninstaller"),
            IsPrimaryButtonEnabled = canLaunch,
            CloseButtonText = Loc.T("Annuler", "Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;
        if (store.LaunchAppUninstaller(app)) return true;

        await new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Loc.T("Désinstallation non lancée", "Uninstaller could not start"),
            Content = Loc.T("Le désinstalleur n'a pas pu démarrer. Aucun dossier n'a été supprimé. Réessayez dans les Paramètres Windows.",
                "The uninstaller could not start. No folders were deleted. Try again from Windows Settings."),
            CloseButtonText = "OK",
        }.ShowAsync();
        return false;
    }
}
