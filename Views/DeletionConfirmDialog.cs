using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Views;

/// Purge for Windows: confirms moving selected items to the
/// Windows Recycle Bin, highlighting any "Check first" (Medium) items at the top.
public static class DeletionConfirmDialog
{
    public sealed record Candidate(string Title, string Subtitle, long Bytes, SafetyLevel Level);

    public static async Task<bool> ConfirmAsync(XamlRoot? xamlRoot, IReadOnlyList<Candidate> candidates)
    {
        if (xamlRoot is null || candidates.Count == 0) return false;

        long totalBytes = candidates.Sum(c => c.Bytes);
        int checkCount = candidates.Count(c => c.Level != SafetyLevel.Safe);

        var rootPanel = new StackPanel { Spacing = 12, MinWidth = 440, MaxWidth = 520 };

        rootPanel.Children.Add(new TextBlock
        {
            Text = Loc.T(
                "Purge envoie ces éléments dans la Corbeille Windows. Vous pourrez les restaurer si vous changez d'avis.",
                "Purge moves these items to the Windows Recycle Bin. You can restore anything if you change your mind."),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemeBrushes.Get("TextSecondary"),
        });

        if (checkCount > 0)
        {
            var warnBorder = new Border
            {
                Background = ThemeBrushes.Get("StatusCheckFill"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
            };
            warnBorder.Child = new TextBlock
            {
                Text = Loc.T(
                    $"⚠ {checkCount} élément{(checkCount > 1 ? "s" : "")} marqué{(checkCount > 1 ? "s" : "")} « À vérifier » : assurez-vous de ne plus en avoir besoin.",
                    $"⚠ {checkCount} item{(checkCount > 1 ? "s" : "")} marked 'Check first': make sure you won't miss {(checkCount > 1 ? "them" : "it")}."),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrushes.Get("StatusCheckText"),
            };
            rootPanel.Children.Add(warnBorder);
        }

        var listStack = new StackPanel { Spacing = 6 };
        foreach (var item in candidates.OrderByDescending(c => c.Level != SafetyLevel.Safe).ThenByDescending(c => c.Bytes).Take(25))
        {
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Spacing = 2 };
            left.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = ThemeBrushes.Get("TextPrimary"),
            });
            left.Children.Add(new TextBlock
            {
                Text = item.Subtitle,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = ThemeBrushes.Get("TextTertiary"),
            });

            var right = new TextBlock
            {
                Text = ByteFormatter.Format(item.Bytes),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.Get("TextSecondary"),
            };
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);

            listStack.Children.Add(new Border
            {
                Background = ThemeBrushes.Get("FillSecondary"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Child = grid,
            });
        }

        rootPanel.Children.Add(new ScrollViewer
        {
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = listStack,
        });

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            RequestedTheme = ThemeBrushes.IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            Title = Loc.T(
                $"Envoyer {candidates.Count} élément{(candidates.Count > 1 ? "s" : "")} ({ByteFormatter.Format(totalBytes)}) à la Corbeille ?",
                $"Move {candidates.Count} item{(candidates.Count > 1 ? "s" : "")} ({ByteFormatter.Format(totalBytes)}) to Recycle Bin?"),
            Content = rootPanel,
            PrimaryButtonText = Loc.T(
                $"Envoyer à la Corbeille ({ByteFormatter.Format(totalBytes)})",
                $"Move to Recycle Bin ({ByteFormatter.Format(totalBytes)})"),
            CloseButtonText = Loc.T("Annuler", "Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var store = PurgeWin.ViewModels.PurgeStore.Shared;
        var empty = new CheckBox { IsChecked = store.AutoEmptyRecycleBin,
            Content = Loc.T("Vider aussi la Corbeille après nettoyage", "Also empty Recycle Bin after cleaning") };
        rootPanel.Children.Add(empty);
        rootPanel.Children.Add(new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Text = Loc.T("Si coché : suppression définitive de toute la Corbeille du périmètre, anciens éléments compris.",
                "When checked: permanently empties the entire Recycle Bin in this scope, including older items."),
            Foreground = ThemeBrushes.Get("StatusDangerText") });
        object initialTitle = dlg.Title;
        void UpdateAction()
        {
            bool permanent = empty.IsChecked == true;
            dlg.DefaultButton = permanent ? ContentDialogButton.Close : ContentDialogButton.Primary;
            dlg.PrimaryButtonText = permanent ? Loc.T("Nettoyer et vider la Corbeille", "Clean and empty Recycle Bin")
                : Loc.T($"Envoyer à la Corbeille ({ByteFormatter.Format(totalBytes)})", $"Move to Recycle Bin ({ByteFormatter.Format(totalBytes)})");
            dlg.Title = permanent ? Loc.T("Nettoyer et vider la Corbeille ?", "Clean and empty Recycle Bin?") : initialTitle;
            if (rootPanel.Children[0] is TextBlock summary) summary.Text = permanent
                ? Loc.T("Le nettoyage sera suivi d'un vidage définitif de toute la Corbeille du périmètre.", "Cleaning will be followed by permanently emptying the entire Recycle Bin in this scope.")
                : Loc.T("Les éléments seront envoyés dans la Corbeille Windows et pourront être restaurés.", "Items will be moved to the Windows Recycle Bin and can be restored.");
        }
        empty.Checked += (_, _) => UpdateAction(); empty.Unchecked += (_, _) => UpdateAction(); UpdateAction();
        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary) return false;
        store.SetAutoEmptyRecycleBin(empty.IsChecked == true);
        return true;
    }
}
