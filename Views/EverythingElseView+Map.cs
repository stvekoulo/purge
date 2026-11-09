using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using PurgeWin.Models;
using PurgeWin.Theme;
using Windows.Foundation;

namespace PurgeWin.Views;

public sealed partial class EverythingElseView
{
    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => RenderTreemap();

    private void OnBucketGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (BucketGrid == null) return;
        bool narrow = e.NewSize.Width < 540;
        if (narrow)
        {
            if (BucketGrid.ColumnDefinitions.Count != 2)
            {
                BucketGrid.ColumnDefinitions.Clear();
                BucketGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
                BucketGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            }
            Grid.SetRow(BucketCard1, 0); Grid.SetColumn(BucketCard1, 0);
            Grid.SetRow(BucketCard2, 0); Grid.SetColumn(BucketCard2, 1);
            Grid.SetRow(BucketCard3, 1); Grid.SetColumn(BucketCard3, 0);
            Grid.SetRow(BucketCard4, 1); Grid.SetColumn(BucketCard4, 1);
        }
        else
        {
            if (BucketGrid.ColumnDefinitions.Count != 4)
            {
                BucketGrid.ColumnDefinitions.Clear();
                for (int i = 0; i < 4; i++)
                    BucketGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
            }
            Grid.SetRow(BucketCard1, 0); Grid.SetColumn(BucketCard1, 0);
            Grid.SetRow(BucketCard2, 0); Grid.SetColumn(BucketCard2, 1);
            Grid.SetRow(BucketCard3, 0); Grid.SetColumn(BucketCard3, 2);
            Grid.SetRow(BucketCard4, 0); Grid.SetColumn(BucketCard4, 3);
        }
    }

    private void OnTreemapPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ExplorerScroll == null) return;
        var delta = e.GetCurrentPoint(ExplorerScroll).Properties.MouseWheelDelta;
        if (delta != 0)
        {
            ExplorerScroll.ChangeView(null, ExplorerScroll.VerticalOffset - delta, null, false);
            e.Handled = true;
        }
    }

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
                EverythingElseBucket.ManagedInApps => "ManagedInApps",
                EverythingElseBucket.YourFiles => "YourFiles",
                EverythingElseBucket.LeftAlone => "LeftAlone",
                EverythingElseBucket.ManagedByWindows => "ManagedByWindows",
                _ => "ChartEverythingElse"
            };
            var text = new StackPanel { Spacing = 3 };
            string foreground = folder.Bucket == EverythingElseBucket.ManagedByWindows ? "ManagedByWindowsLabel" : "BucketLabel";
            if (tile.Frame.Width > 80 && tile.Frame.Height > 40)
            {
                text.Children.Add(new TextBlock
                {
                    Text = folder.DisplayName,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = ThemeBrushes.Get(foreground)
                });
                if (tile.Frame.Height > 58)
                    text.Children.Add(new TextBlock { Text = folder.SizeText, FontSize = 11, Foreground = ThemeBrushes.Get(foreground) });
            }
            var tileBorder = new Border
            {
                Child = text,
                Width = tile.Frame.Width,
                Height = tile.Frame.Height,
                Padding = new(7),
                CornerRadius = new(6),
                Background = ThemeBrushes.Get(fill),
                BorderBrush = ThemeBrushes.Get(_selected?.Id == folder.Id ? "TextPrimary" : "BorderSubtle"),
                BorderThickness = new(_selected?.Id == folder.Id ? 2 : 1),
                IsHitTestVisible = true,
                Tag = folder
            };
            AutomationProperties.SetName(tileBorder, folder.DisplayName + " · " + folder.SizeText);

            var tip = new ToolTip
            {
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = folder.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 },
                        new TextBlock { Text = folder.SizeText, FontSize = 11, Foreground = ThemeBrushes.Get("TextSecondary") },
                        new TextBlock { Text = folder.FullPath, FontSize = 10, Foreground = ThemeBrushes.Get("TextTertiary"), TextWrapping = TextWrapping.Wrap, MaxWidth = 260 }
                    }
                },
                Placement = tile.Frame.Y < 80 ? PlacementMode.Bottom : PlacementMode.Top,
                VerticalOffset = tile.Frame.Y < 80 ? 6 : -6
            };
            ToolTipService.SetToolTip(tileBorder, tip);

            tileBorder.PointerWheelChanged += OnTreemapPointerWheelChanged;
            tileBorder.PointerEntered += (_, _) =>
            {
                ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
                ShowFolderHover(folder);
            };
            tileBorder.PointerExited += (_, _) =>
            {
                ProtectedCursor = null;
                UpdateInspector();
            };

            tileBorder.Tapped += async (_, _) =>
            {
                if (_selected?.Id == folder.Id && folder.IsDirectory && !string.IsNullOrEmpty(folder.FullPath))
                {
                    await NavigateAsync(folder.FullPath);
                }
                else
                {
                    _selected = folder;
                    UpdateInspector();
                    RenderTreemap();
                }
            };
            tileBorder.DoubleTapped += async (_, _) =>
            {
                if (folder.IsDirectory && !string.IsNullOrEmpty(folder.FullPath))
                    await NavigateAsync(folder.FullPath);
            };

            Canvas.SetLeft(tileBorder, tile.Frame.X);
            Canvas.SetTop(tileBorder, tile.Frame.Y);
            TreemapCanvas.Children.Add(tileBorder);
        }
    }

    private void ShowFolderHover(EverythingElseFolder folder)
    {
        if (SelectedNameText == null) return;
        SelectedNameText.Text = folder.DisplayName;
        SelectedSizeText.Text = folder.SizeText;
        SelectedDescText.Text = folder.LocationText;
        SelectedAdviceText.Text = folder.ActionAdvice;
    }
}
