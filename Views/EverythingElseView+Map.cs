using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using PurgeWin.Models;
using PurgeWin.Theme;
using Windows.Foundation;

namespace PurgeWin.Views;

public sealed partial class EverythingElseView
{
    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => RenderTreemap();

    private void RenderTreemap()
    {
        if (TreemapCanvas == null) return;
        TreemapCanvas.Children.Clear();
        double width = TreemapCanvas.ActualWidth, height = TreemapCanvas.ActualHeight;
        if (width < 40 || height < 40 || _visible.Count == 0) return;
        var items = _visible.Where(i => i.Bytes > 0).Take(256).ToArray();
        var tiles = TreemapLayout.Squarify(items.Select(i => new TreemapItem(i.Id, i.Bytes)).ToArray(), new Rect(0, 0, width, height));
        var index = items.ToDictionary(i => i.Id);
        foreach (var tile in tiles)
        {
            var folder = index[tile.Id];
            string fill = folder.Bucket switch
            {
                EverythingElseBucket.ManagedInApps => "ManagedInApps", EverythingElseBucket.YourFiles => "YourFiles",
                EverythingElseBucket.LeftAlone => "LeftAlone", EverythingElseBucket.ManagedByWindows => "ManagedByWindows",
                _ => "ChartEverythingElse"
            };
            var text = new StackPanel { Spacing = 3 };
            string foreground = folder.Bucket == EverythingElseBucket.ManagedByWindows ? "ManagedByWindowsLabel" : "BucketLabel";
            if (tile.Frame.Width > 80 && tile.Frame.Height > 40)
            {
                text.Children.Add(new TextBlock { Text = folder.DisplayName, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = ThemeBrushes.Get(foreground) });
                if (tile.Frame.Height > 58) text.Children.Add(new TextBlock { Text = folder.SizeText, FontSize = 11, Foreground = ThemeBrushes.Get(foreground) });
            }
            var surface = new Border { Child = text, Padding = new(7), CornerRadius = new(6), Background = ThemeBrushes.Get(fill),
                BorderBrush = ThemeBrushes.Get("TextPrimary"), BorderThickness = new(_selected?.Id == folder.Id ? 2 : 0),
            };
            var button = new Button { Content = surface, Width = tile.Frame.Width, Height = tile.Frame.Height,
                MinWidth = 0, MinHeight = 0, CornerRadius = new(6), Padding = new(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = ThemeBrushes.Get("SurfaceCard"), BorderThickness = new(0),
                IsTabStop = tile.Frame.Width > 80 && tile.Frame.Height > 40, Tag = folder };
            AutomationProperties.SetName(button, folder.DisplayName + " · " + folder.SizeText);
            ToolTipService.SetToolTip(button, folder.DisplayName + "\n" + folder.SizeText + "\n" + folder.FullPath);
            button.Click += (_, _) => { _selected = folder; UpdateInspector(); RenderTreemap(); };
            button.DoubleTapped += OnFolderDoubleTapped;
            Canvas.SetLeft(button, tile.Frame.X); Canvas.SetTop(button, tile.Frame.Y);
            TreemapCanvas.Children.Add(button);
        }
    }
}
