using System.Diagnostics;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

/// Dialog confirming application uninstallation and leftover removal to the Recycle Bin.
/// Directly mirrors the reference review sheet design.
public static class UninstallReviewDialog
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot, InstalledAppItem app, PurgeStore store)
    {
        if (xamlRoot is null) return false;

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = Loc.T($"Désinstaller {app.DisplayName} ?", $"Uninstall {app.DisplayName}?"),
            PrimaryButtonText = Loc.T("Déplacer vers la Corbeille", "Move to Recycle Bin"),
            CloseButtonText = Loc.T("Annuler", "Cancel"),
            DefaultButton = ContentDialogButton.Primary
        };

        var root = new StackPanel { Spacing = 14, MinWidth = 460, MaxWidth = 540 };

        root.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Purge déplace l'application et les éléments cochés vers la Corbeille. Rien n'est supprimé définitivement, vous pouvez tout restaurer si vous changez d'avis.",
                "Purge moves the app and any checked items to the Recycle Bin. Nothing is deleted permanently, so you can restore anything if you change your mind."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Get("TextSecondary")
        });

        var scroll = new ScrollViewer { MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var listStack = new StackPanel { Spacing = 8 };

        // 1. Leftover data folders list initialized first for clean closure
        var leftoverChecks = new List<(AppLeftoverItem item, bool isChecked)>();
        foreach (var l in app.Leftovers)
            leftoverChecks.Add((l, true));

        // 2. Main Application row
        bool appChecked = true;
        long currentTotal = (app.AppBytes > 0 ? app.AppBytes : 0) + app.Leftovers.Sum(l => l.Bytes);
        var totalText = new TextBlock
        {
            Text = Loc.T($"Déplacement de {ByteFormatter.Format(currentTotal)} vers la Corbeille", $"Moving {ByteFormatter.Format(currentTotal)} to Recycle Bin"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeBrushes.Get("TextSecondary"),
            Margin = new Thickness(0, 8, 0, 0)
        };

        void Recalculate()
        {
            long sum = 0;
            if (appChecked && app.AppBytes > 0) sum += app.AppBytes;
            foreach (var (it, c) in leftoverChecks)
            {
                if (c) sum += it.Bytes;
            }
            totalText.Text = Loc.T($"Déplacement de {ByteFormatter.Format(sum)} vers la Corbeille", $"Moving {ByteFormatter.Format(sum)} to Recycle Bin");
        }

        var appRow = CreateItemRow(
            app.DisplayName,
            !string.IsNullOrWhiteSpace(app.InstallLocation) ? app.InstallLocation : Loc.T("Désinstallation standard", "Standard uninstaller"),
            app.AppBytes,
            app.IconPath,
            true,
            isChecked =>
            {
                appChecked = isChecked;
                Recalculate();
            });
        listStack.Children.Add(appRow);

        // 3. Leftover rows
        for (int i = 0; i < leftoverChecks.Count; i++)
        {
            int idx = i;
            var l = leftoverChecks[i].item;
            var row = CreateItemRow(
                l.ScopeLabel,
                l.Path,
                l.Bytes,
                null,
                true,
                isChecked =>
                {
                    leftoverChecks[idx] = (l, isChecked);
                    Recalculate();
                });
            listStack.Children.Add(row);
        }

        scroll.Content = listStack;
        root.Children.Add(scroll);
        root.Children.Add(totalText);
        dialog.Content = root;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return false;

        // Perform uninstallation & trash moves
        var moved = new List<string>();
        long freed = 0;

        foreach (var (it, c) in leftoverChecks)
        {
            if (c && Directory.Exists(it.Path) && !DeletionSafetyPolicy.IsProtected(it.Path))
            {
                try
                {
                    var err = RecycleBin.MoveToRecycleBin(it.Path);
                    if (err is null)
                    {
                        moved.Add(it.Path);
                        freed += it.Bytes;
                    }
                }
                catch { }
            }
        }

        if (appChecked)
        {
            if (!string.IsNullOrWhiteSpace(app.InstallLocation)
                && Directory.Exists(app.InstallLocation)
                && !DeletionSafetyPolicy.IsProtected(app.InstallLocation))
            {
                try
                {
                    var err = RecycleBin.MoveToRecycleBin(app.InstallLocation);
                    if (err is null)
                    {
                        moved.Add(app.InstallLocation);
                        freed += app.AppBytes;
                    }
                }
                catch { }
            }
            else if (!string.IsNullOrWhiteSpace(app.UninstallString))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c {app.UninstallString}") { CreateNoWindow = true, UseShellExecute = false });
                }
                catch { }
            }
        }

        store.InstalledApps.Remove(app);
        store.ApplyUninstallFilter();
        if (freed > 0 || moved.Count > 0)
        {
            var outcome = new CleanOutcome(freed, moved.Count, moved, []);
            store.RecordOutcome(outcome, "uninstaller");
        }
        return true;
    }

    private static Border CreateItemRow(string title, string subtitle, long bytes, string? iconPath, bool initialChecked, Action<bool> onToggle)
    {
        var border = new Border
        {
            Background = ThemeBrushes.Get("SurfaceElevated"),
            BorderBrush = ThemeBrushes.Get("BorderSubtle"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10)
        };

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox { IsChecked = initialChecked, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        check.Checked += (_, _) => onToggle(true);
        check.Unchecked += (_, _) => onToggle(false);
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var iconHost = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(6),
            Background = ThemeBrushes.Get("FillSecondary"),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            iconHost.Child = new Image
            {
                Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath)),
                Width = 28,
                Height = 28,
                Stretch = Stretch.Uniform
            };
        }
        else
        {
            iconHost.Child = new FontIcon
            {
                Glyph = "\uE71D",
                FontSize = 14,
                Foreground = ThemeBrushes.Get("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        Grid.SetColumn(iconHost, 1);
        grid.Children.Add(iconHost);

        var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
        textStack.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, Foreground = ThemeBrushes.Get("TextSecondary"), TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(textStack, 2);
        grid.Children.Add(textStack);

        var sizeText = new TextBlock
        {
            Text = bytes > 0 ? ByteFormatter.Format(bytes) : "",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeBrushes.Get("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(sizeText, 3);
        grid.Children.Add(sizeText);

        border.Child = grid;
        return border;
    }
}
