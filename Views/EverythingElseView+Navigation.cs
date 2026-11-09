using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        if (FolderRows == null) return;
        string search = FolderSearch.Text?.Trim() ?? "";
        var query = _entries.Where(i => _bucket == 0 || (int)i.Bucket == _bucket - 1)
            .Where(i => search.Length == 0 || i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || i.FullPath.Contains(search, StringComparison.OrdinalIgnoreCase));
        _visible = (_sort == 1 ? query.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase) : query.OrderByDescending(i => i.Bytes)).ToList();
        FolderRows.ItemsSource = _visible.Take(_limit).ToArray();
        FolderCount.Text = Loc.T($"{_visible.Count} élément{(_visible.Count == 1 ? "" : "s")} · {Math.Min(_limit, _visible.Count)} affiché{(_visible.Count == 1 ? "" : "s")}",
            $"{_visible.Count} item{(_visible.Count == 1 ? "" : "s")} · {Math.Min(_limit, _visible.Count)} shown");
        MoreButton.Visibility = _visible.Count > _limit ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = _visible.Count == 0 && !_busy ? Visibility.Visible : Visibility.Collapsed;
        MapCard.Visibility = MapToggle.IsOn && _visible.Any(i => i.Bytes > 0) ? Visibility.Visible : Visibility.Collapsed;
        if (_selected != null && !_visible.Any(i => i.Id == _selected.Id)) _selected = _visible.FirstOrDefault();
        Bucket1Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.ManagedInApps).Sum(i => i.Bytes));
        Bucket2Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.YourFiles).Sum(i => i.Bytes));
        Bucket3Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.LeftAlone).Sum(i => i.Bytes));
        Bucket4Size.Text = ByteFormatter.Format(_entries.Where(i => i.Bucket == EverythingElseBucket.ManagedByWindows).Sum(i => i.Bytes));
        _syncing = true; BucketPicker.SelectedIndex = _bucket; _syncing = false;
        UpdateInspector(); RenderTreemap();
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
        ParentBtn.IsEnabled = _path != null || _history.Count > 0;
    }

    private void AddCrumb(string label, string? path)
    {
        var button = new Button { Content = label, Padding = new(8, 4, 8, 4), FontSize = 12, CornerRadius = new(6) };
        button.Click += async (_, _) => await NavigateAsync(path);
        Breadcrumbs.Children.Add(button);
    }

    private void UpdateInspector()
    {
        if (SelectedNameText == null) return;
        SelectedNameText.Text = _selected?.DisplayName ?? Loc.T("Sélectionnez un élément", "Select an item");
        SelectedSizeText.Text = _selected?.SizeText ?? "";
        SelectedDescText.Text = _selected?.LocationText ?? Loc.T("Cliquez sur une tuile ou une ligne pour voir les détails.", "Click a tile or row to see details.");
        SelectedAdviceText.Text = _selected?.ActionAdvice ?? "";
        ExploreBtn.IsEnabled = _selected is { IsDirectory: true, FullPath.Length: > 0 };
        RevealBtn.IsEnabled = CopyPathBtn.IsEnabled = !string.IsNullOrEmpty(_selected?.FullPath);
        ToolTipService.SetToolTip(CopyPathBtn, Loc.T("Copier le chemin", "Copy path"));
    }

    private void OnFolderSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EverythingElseFolder entry })
        {
            _selected = entry; UpdateInspector(); RenderTreemap();
        }
    }
    private async void OnFolderDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is Button { Tag: EverythingElseFolder { IsDirectory: true } entry }) await NavigateAsync(entry.FullPath);
    }
    private async void OnExploreClick(object sender, RoutedEventArgs e)
    {
        if (_selected is { IsDirectory: true } entry) await NavigateAsync(entry.FullPath);
    }
    private async void OnNavigateFolder(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EverythingElseFolder { IsDirectory: true } entry }) await NavigateAsync(entry.FullPath);
    }
    private void OnRevealSelectedClick(object sender, RoutedEventArgs e) => ExplorerReveal.Show(_selected?.FullPath);
    private void OnCopyPath(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selected?.FullPath)) return;
        var data = new DataPackage(); data.SetText(_selected.FullPath); Clipboard.SetContent(data);
    }
    private void OnSearchChanged(object sender, TextChangedEventArgs e) { _limit = 50; if (!_syncing) RefreshEntries(); }
    private void OnBucketChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && BucketPicker.SelectedIndex >= 0) { _bucket = BucketPicker.SelectedIndex; _limit = 50; RefreshEntries(); }
    }
    private void OnBucketClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out int bucket))
        { _bucket = _bucket == bucket + 1 ? 0 : bucket + 1; _limit = 50; RefreshEntries(); }
    }
    private void OnSortChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && SortPicker.SelectedIndex >= 0) { _sort = SortPicker.SelectedIndex; RefreshEntries(); }
    }
    private void OnMore(object sender, RoutedEventArgs e) { _limit += 50; RefreshEntries(); }
    private void OnMapToggled(object sender, RoutedEventArgs e) { if (IsLoaded) RefreshEntries(); }
}
