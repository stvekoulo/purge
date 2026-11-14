using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

/// <summary>
/// Confirmation dialog for single or multi-app batch uninstallation with
/// automatic cleanup of associated AppData leftovers and caches.
/// </summary>
public static class UninstallReviewDialog
{
    public static Task<bool> ShowAsync(XamlRoot xamlRoot, InstalledAppItem app, PurgeStore store) =>
        ShowBatchAsync(xamlRoot, [app], store);

    public static async Task<bool> ShowBatchAsync(
        XamlRoot xamlRoot, IReadOnlyList<InstalledAppItem> apps, PurgeStore store)
    {
        if (xamlRoot is null || apps.Count == 0) return false;

        long totalBytes = apps.Sum(a => a.TotalBytes);
        long leftoverBytes = apps.Sum(a => a.LeftoverBytes);
        int leftoverFolders = apps.Sum(a => a.Leftovers.Count);
        bool anyCanUninstall = apps.Any(a => a.CanUninstall);

        var panel = new StackPanel { Spacing = 12, MinWidth = 440, MaxWidth = 540 };

        panel.Children.Add(new TextBlock
        {
            Text = apps.Count == 1
                ? Loc.T(
                    "Purge va désinstaller cette application puis nettoyer automatiquement ses caches et dossiers résiduels en une seule opération.",
                    "Purge will uninstall this application and automatically clean its associated caches and leftover folders in a single pass.")
                : Loc.T(
                    $"Purge va désinstaller ces {apps.Count} applications à la suite, puis nettoyer automatiquement tous leurs caches et dossiers résiduels en même temps.",
                    $"Purge will uninstall all {apps.Count} selected applications sequentially, then automatically clean their caches and leftover folders at once."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Get("TextSecondary"),
        });

        var listStack = new StackPanel { Spacing = 6 };
        foreach (var app in apps)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Spacing = 1 };
            titleStack.Children.Add(new TextBlock
            {
                Text = app.DisplayName,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            if (app.Leftovers.Count > 0)
            {
                titleStack.Children.Add(new TextBlock
                {
                    Text = Loc.T(
                        $"{app.Leftovers.Count} dossier(s) AppData/cache ({ByteFormatter.Format(app.LeftoverBytes)})",
                        $"{app.Leftovers.Count} AppData/cache folder(s) ({ByteFormatter.Format(app.LeftoverBytes)})"),
                    FontSize = 11,
                    Foreground = ThemeBrushes.Get("TextTertiary"),
                });
            }
            Grid.SetColumn(titleStack, 0);
            row.Children.Add(titleStack);

            var sizeTxt = new TextBlock
            {
                Text = app.TotalSizeText,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.Get("TextSecondary"),
            };
            Grid.SetColumn(sizeTxt, 1);
            row.Children.Add(sizeTxt);

            listStack.Children.Add(new Border
            {
                Background = ThemeBrushes.Get("FillSecondary"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Child = row,
            });
        }

        panel.Children.Add(new ScrollViewer
        {
            MaxHeight = 210,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = listStack,
        });

        var cleanLeftoversCheck = new CheckBox
        {
            IsChecked = true,
            Content = leftoverFolders > 0
                ? Loc.T(
                    $"Nettoyer aussi les caches et dossiers résiduels ({leftoverFolders} dossier(s) · {ByteFormatter.Format(leftoverBytes)})",
                    $"Also clean associated caches and leftover folders ({leftoverFolders} folder(s) · {ByteFormatter.Format(leftoverBytes)})")
                : Loc.T(
                    "Nettoyer automatiquement les caches et dossiers résiduels après désinstallation",
                    "Automatically clean caches and leftover folders after uninstallation"),
            FontSize = 12,
        };
        panel.Children.Add(cleanLeftoversCheck);

        var silentCheck = new CheckBox
        {
            IsChecked = true,
            Content = Loc.T(
                "Désinstallation automatique sans intervention (mode silencieux si supporté)",
                "Unattended automatic uninstallation (silent mode when supported)"),
            FontSize = 12,
        };
        panel.Children.Add(silentCheck);

        var title = apps.Count == 1
            ? Loc.T($"Désinstaller {apps[0].DisplayName} ?", $"Uninstall {apps[0].DisplayName}?")
            : Loc.T($"Désinstaller {apps.Count} applications ({ByteFormatter.Format(totalBytes)}) ?",
                    $"Uninstall {apps.Count} applications ({ByteFormatter.Format(totalBytes)})?");

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ThemeBrushes.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            Title = title,
            Content = panel,
            PrimaryButtonText = apps.Count == 1
                ? Loc.T("Désinstaller et nettoyer", "Uninstall & clean")
                : Loc.T($"Tout désinstaller ({apps.Count})", $"Uninstall all ({apps.Count})"),
            IsPrimaryButtonEnabled = anyCanUninstall || leftoverFolders > 0,
            CloseButtonText = Loc.T("Annuler", "Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;

        await store.UninstallAppsAndCleanAsync(
            apps,
            cleanLeftovers: cleanLeftoversCheck.IsChecked == true,
            preferUnattended: silentCheck.IsChecked == true);
        return true;
    }
}
