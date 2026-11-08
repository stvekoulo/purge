using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;
using Windows.Foundation;

namespace PurgeWin.Views;

public sealed partial class EverythingElseView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;
    private EverythingElseFolder? _selected;

    public EverythingElseView()
    {
        InitializeComponent();
        Store.EverythingElseFolders.CollectionChanged += (_, _) => SyncUi();
        Loc.LanguageChanged += SyncUi;
        ThemeBrushes.ThemeChanged += SyncUi;
        Loaded += (_, _) =>
        {
            if (Store.EverythingElseFolders.Count == 0) _ = Store.ScanEverythingElseAsync();
            SyncUi();
        };
    }

    private void SyncUi()
    {
        BackBtn.Content = Loc.T("← Vue d'ensemble", "← Overview");
        PageTitle.Text = Loc.T("Tout le reste", "Everything else");
        PageSubtitle.Text = Loc.T(
            "Carte en lecture seule de ce que Purge ne nettoie pas automatiquement.",
            "Read-only map of disk space that Purge does not clean automatically.");
        RefreshBtn.Content = Loc.T("Actualiser", "Refresh");

        Bucket1Title.Text = Loc.T("Géré dans les apps", "Managed in apps");
        Bucket2Title.Text = Loc.T("Vos fichiers", "Your files");
        Bucket3Title.Text = Loc.T("Laissé par Purge", "Left alone by Purge");
        Bucket4Title.Text = Loc.T("Géré par Windows", "Managed by Windows");
        RevealBtn.Content = Loc.T("Afficher dans l'Explorateur", "Reveal in Explorer");

        long b1 = Store.EverythingElseFolders.Where(f => f.Bucket == EverythingElseBucket.ManagedInApps).Sum(f => f.Bytes);
        long b2 = Store.EverythingElseFolders.Where(f => f.Bucket == EverythingElseBucket.YourFiles).Sum(f => f.Bytes);
        long b3 = Store.EverythingElseFolders.Where(f => f.Bucket == EverythingElseBucket.LeftAlone).Sum(f => f.Bytes);
        long b4 = Store.EverythingElseFolders.Where(f => f.Bucket == EverythingElseBucket.ManagedByWindows).Sum(f => f.Bytes);

        Bucket1Size.Text = ByteFormatter.Format(b1);
        Bucket2Size.Text = ByteFormatter.Format(b2);
        Bucket3Size.Text = ByteFormatter.Format(b3);
        Bucket4Size.Text = ByteFormatter.Format(b4);

        if (_selected is null && Store.EverythingElseFolders.Count > 0)
            _selected = Store.EverythingElseFolders[0];

        UpdateInspector();
        RenderTreemap();
    }

    private void UpdateInspector()
    {
        if (_selected is null)
        {
            SelectedNameText.Text = Loc.T("Sélectionnez une tuile", "Select a tile");
            SelectedSizeText.Text = "";
            SelectedDescText.Text = Loc.T("Cliquez sur un bloc de la carte pour voir les détails.", "Click any block in the treemap to inspect details.");
            SelectedAdviceText.Text = "";
            RevealBtn.IsEnabled = false;
            return;
        }
        SelectedNameText.Text = _selected.DisplayName;
        SelectedSizeText.Text = _selected.SizeText;
        SelectedDescText.Text = _selected.Description;
        SelectedAdviceText.Text = _selected.ActionAdvice;
        RevealBtn.IsEnabled = !string.IsNullOrEmpty(_selected.FullPath);
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e) => RenderTreemap();

    private void RenderTreemap()
    {
        if (TreemapCanvas is null) return;
        TreemapCanvas.Children.Clear();
        double w = TreemapCanvas.ActualWidth;
        double h = TreemapCanvas.ActualHeight;
        if (w < 40 || h < 40 || Store.EverythingElseFolders.Count == 0) return;

        var treemapItems = Store.EverythingElseFolders
            .Select(f => new TreemapItem(f.Id, Math.Max(1, f.Bytes)))
            .ToList();
        var tiles = TreemapLayout.Squarify(treemapItems, new Rect(0, 0, w, h), spacing: 3.0);
        var byId = Store.EverythingElseFolders.ToDictionary(f => f.Id);

        foreach (var tile in tiles)
        {
            if (!byId.TryGetValue(tile.Id, out var folder)) continue;
            var fillKey = folder.Bucket switch
            {
                EverythingElseBucket.ManagedInApps => "ManagedInApps",
                EverythingElseBucket.YourFiles => "YourFiles",
                EverythingElseBucket.LeftAlone => "LeftAlone",
                _ => "ManagedByWindows",
            };
            var fgKey = folder.Bucket == EverythingElseBucket.ManagedByWindows ? "ManagedByWindowsLabel" : "BucketLabel";
            bool isSel = _selected?.Id == folder.Id;

            var border = new Border
            {
                Width = Math.Max(0, tile.Frame.Width),
                Height = Math.Max(0, tile.Frame.Height),
                CornerRadius = new CornerRadius(6),
                Background = ThemeBrushes.Get(fillKey),
                BorderBrush = ThemeBrushes.Get(isSel ? "TextPrimary" : "Hairline"),
                BorderThickness = new Thickness(isSel ? 2 : 0),
                Padding = new Thickness(8, 6, 8, 6),
                Tag = folder,
            };

            if (tile.Frame.Width > 70 && tile.Frame.Height > 36)
            {
                var stack = new StackPanel { Spacing = 2 };
                stack.Children.Add(new TextBlock
                {
                    Text = folder.DisplayName,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeBrushes.Get(fgKey),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                if (tile.Frame.Height > 52)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = folder.SizeText,
                        FontSize = 11,
                        Foreground = ThemeBrushes.Get(fgKey),
                        Opacity = 0.88,
                    });
                }
                border.Child = stack;
            }

            border.PointerPressed += (_, _) =>
            {
                _selected = folder;
                UpdateInspector();
                RenderTreemap();
            };

            Canvas.SetLeft(border, tile.Frame.X);
            Canvas.SetTop(border, tile.Frame.Y);
            TreemapCanvas.Children.Add(border);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) =>
        Store.OverviewPage = OverviewSubPage.Summary;

    private void OnRefreshClick(object sender, RoutedEventArgs e) =>
        _ = Store.ScanEverythingElseAsync();

    private void OnRevealSelectedClick(object sender, RoutedEventArgs e)
    {
        if (_selected is not null && !string.IsNullOrEmpty(_selected.FullPath))
            ExplorerReveal.Show(_selected.FullPath);
    }
}
