using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Views;

public sealed partial class OverviewView
{
    private void RebuildDriveButtons()
    {
        if (DrivePillsPanel == null) return;
        DrivePillsPanel.Children.Clear();

        var drives = Store.Drives.ToArray();
        foreach (var drive in drives)
        {
            bool isActive = drive.IsAllDrives
                ? Store.IsAllDrives
                : (!Store.IsAllDrives && string.Equals(Store.Settings.SelectedDrive, drive.Key, StringComparison.OrdinalIgnoreCase));

            string label = drive.IsAllDrives
                ? $"{drive.DisplayTitle} ({ByteFormatter.Format(drive.FreeBytes)} {Loc.T("libres", "free")})"
                : $"{drive.DisplayTitle} · {ByteFormatter.Format(drive.UsedBytes)} / {ByteFormatter.Format(drive.TotalBytes)}";

            var textBlock = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = ThemeBrushes.Get(isActive ? "TextPrimary" : "TextSecondary"),
                VerticalAlignment = VerticalAlignment.Center
            };

            var pill = new Button
            {
                Content = textBlock,
                Padding = new Thickness(12, 5, 12, 5),
                CornerRadius = new CornerRadius(14),
                Background = ThemeBrushes.Get(isActive ? "FillSecondary" : "Transparent"),
                BorderBrush = isActive ? ThemeBrushes.Get("BorderSubtle") : new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(1),
                IsEnabled = !Store.IsCleaning && drive.IsAvailable
            };

            AutomationProperties.SetName(pill, label);
            ToolTipService.SetToolTip(pill, drive.IsAllDrives
                ? Loc.T("Analyser et afficher tous les disques", "Scan and show all drives")
                : Loc.T("Afficher uniquement ce disque dans toutes les vues", "Show only this drive in all views"));

            pill.Click += (_, _) =>
            {
                Store.SetDrive(drive.Key);
                RebuildDriveButtons();
            };

            DrivePillsPanel.Children.Add(pill);
        }
    }
}
