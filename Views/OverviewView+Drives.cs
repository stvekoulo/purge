using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using PurgeWin.Theme;
using PurgeWin.Utilities;

namespace PurgeWin.Views;

public sealed partial class OverviewView
{
    private int _driveColumns;

    private void OnDriveCardsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        int columns = e.NewSize.Width < 460 ? 1 : 2;
        if (_driveColumns != columns) { _driveColumns = columns; RebuildDriveButtons(); }
    }

    private void RebuildDriveButtons()
    {
        if (DriveButtonsPanel == null) return;
        var drives = Store.Drives.Where(d => !d.IsAllDrives).ToArray();
        int columns = Math.Max(1, _driveColumns);
        DriveButtonsPanel.Children.Clear();
        DriveButtonsPanel.ColumnDefinitions.Clear();
        DriveButtonsPanel.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++) DriveButtonsPanel.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        for (int i = 0; i < drives.Length; i++)
        {
            var drive = drives[i];
            if (i % columns == 0) DriveButtonsPanel.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var content = new StackPanel { Spacing = 7 };
            content.Children.Add(new TextBlock { Text = drive.DisplayTitle, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = drive.UsedPercent, Height = 4 });
            content.Children.Add(new TextBlock { Text = drive.CapacitySummary, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrushes.Get("TextSecondary") });
            var button = new Button { Content = content, Padding = new(12), CornerRadius = new(8),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = ThemeBrushes.Get(drive.IsActive ? "FillSecondary" : "SurfaceCard"),
                BorderBrush = ThemeBrushes.Get(drive.IsActive ? "ActionPrimary" : "BorderSubtle"),
                BorderThickness = new(drive.IsActive ? 2 : 1), IsEnabled = !Store.IsCleaning && drive.IsAvailable };
            AutomationProperties.SetName(button, drive.DisplayTitle + " · " + drive.CapacitySummary);
            ToolTipService.SetToolTip(button, Loc.T("Afficher uniquement ce disque dans toutes les vues", "Show only this drive in all views"));
            button.Click += (_, _) => Store.SetDrive(drive.Key);
            Grid.SetColumn(button, i % columns); Grid.SetRow(button, i / columns);
            DriveButtonsPanel.Children.Add(button);
        }
    }
}
