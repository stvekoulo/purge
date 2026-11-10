using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using Windows.ApplicationModel.DataTransfer;

namespace PurgeWin.Views;

public sealed partial class EverythingElseView
{
    private void RefreshEntries()
    {
        string search = FolderSearch?.Text?.Trim() ?? "";
        var query = _entries.Where(i => _bucket == 0 || (int)i.Bucket == _bucket - 1)
            .Where(i => search.Length == 0 || i.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || i.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase));
        _visible = query.OrderByDescending(i => i.Bytes).ToList();
        if (_selected != null && !_visible.Any(i => i.Id == _selected.Id)) _selected = _visible.FirstOrDefault();

        if (Bucket1Size != null) Bucket1Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.ManagedInApps).Sum(i => i.Bytes));
        if (Bucket2Size != null) Bucket2Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.YourFiles).Sum(i => i.Bytes));
        if (Bucket3Size != null) Bucket3Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.LeftAlone).Sum(i => i.Bytes));
        if (Bucket4Size != null) Bucket4Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.ManagedByWindows).Sum(i => i.Bytes));

        _syncing = true;
        if (BucketPicker != null) BucketPicker.SelectedIndex = _bucket;
        _syncing = false;
        UpdateInspector();
        RenderTreemap();
    }

    private void BuildBreadcrumbs()
    {
        if (Breadcrumbs == null) return;
        Breadcrumbs.Children.Clear();
        AddCrumb(Store.ScopeTitle, null);
        if (_path != null)
        {
            var root = Path.GetPathRoot(_path)!;
            AddCrumb(root.TrimEnd('\\'), root);
            var current = root;
            foreach (var part in _path[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                AddCrumb(part, current);
            }
        }
        if (ParentBtn != null) ParentBtn.IsEnabled = _path != null || _history.Count > 0;
    }

    private void AddCrumb(string label, string? path)
    {
        var button = new Button { Content = label, Padding = new(8, 4, 8, 4), FontSize = 12, CornerRadius = new(6) };
        button.Click += async (_, _) => await NavigateAsync(path);
        Breadcrumbs?.Children.Add(button);
    }

    private void UpdateInspector()
    {
        if (SelectedNameText == null) return;
        SelectedNameText.Text = _selected?.DisplayName ?? Loc.T("Sélectionnez un élément", "Select an item");
        SelectedSizeText.Text = _selected?.SizeText ?? "";
        SelectedDescText.Text = _selected?.LocationText ?? Loc.T("Cliquez sur une tuile pour voir les détails ou double-cliquez pour explorer.", "Click a tile to see details or double-click to explore.");
        SelectedAdviceText.Text = _selected?.ActionAdvice ?? "";
        if (ExploreBtn != null) ExploreBtn.IsEnabled = _selected is { IsDirectory: true, FullPath.Length: > 0 };
        if (RevealBtn != null) RevealBtn.IsEnabled = !string.IsNullOrEmpty(_selected?.FullPath);
        if (CopyPathBtn != null)
        {
            CopyPathBtn.IsEnabled = !string.IsNullOrEmpty(_selected?.FullPath);
            ToolTipService.SetToolTip(CopyPathBtn, Loc.T("Copier le chemin", "Copy path"));
        }
    }

    private async void OnExploreClick(object sender, RoutedEventArgs e)
    {
        if (_selected is { IsDirectory: true } entry) await NavigateAsync(entry.FullPath);
    }

    private void OnRevealSelectedClick(object sender, RoutedEventArgs e) => ExplorerReveal.Show(_selected?.FullPath);

    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selected?.FullPath)) return;
        var data = new DataPackage(); data.SetText(_selected.FullPath); Clipboard.SetContent(data);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { if (!_syncing) RefreshEntries(); }

    private void OnBucketChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && BucketPicker.SelectedIndex >= 0) { _bucket = BucketPicker.SelectedIndex; RefreshEntries(); }
    }

    private void OnBucketClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int bucket))
        {
            _bucket = _bucket == bucket + 1 ? 0 : bucket + 1;
            RefreshEntries();
        }
    }
}
