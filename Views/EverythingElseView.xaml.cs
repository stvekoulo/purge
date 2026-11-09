using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Theme;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class EverythingElseView : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;
    private List<EverythingElseFolder> _entries = [];
    private List<EverythingElseFolder> _visible = [];
    private EverythingElseFolder? _selected;
    private string? _path;
    private readonly Stack<(string? Path, double Offset)> _history = new();
    private CancellationTokenSource? _load;
    private long _revision = -1;
    private int _limit = 50;
    private bool _syncing, _busy;
    private int _bucket, _sort;

    public EverythingElseView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Store.PropertyChanged += OnStoreChanged;
            Store.EverythingElseFolders.CollectionChanged += OnRootChanged;
            Loc.LanguageChanged += SyncTexts;
            ThemeBrushes.ThemeChanged += RenderTreemap;
            SyncTexts(); ResetScope();
        };
        Unloaded += (_, _) =>
        {
            Store.PropertyChanged -= OnStoreChanged;
            Store.EverythingElseFolders.CollectionChanged -= OnRootChanged;
            Loc.LanguageChanged -= SyncTexts;
            ThemeBrushes.ThemeChanged -= RenderTreemap;
            _load?.Cancel();
        };
    }

    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurgeStore.ScopeRevision) && _revision != Store.ScopeRevision) ResetScope();
    }
    private void OnRootChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_path == null && !_busy && Store.EverythingElseFolders.Count > 0) SetEntries(Store.EverythingElseFolders);
    }

    private void ResetScope()
    {
        _revision = Store.ScopeRevision;
        _load?.Cancel(); _history.Clear(); _path = null; _selected = null;
        _bucket = 0; FolderSearch.Text = "";
        if (Store.EverythingElseFolders.Count > 0) { _busy = false; SetEntries(Store.EverythingElseFolders); }
        else _ = NavigateAsync(null, false);
        BuildBreadcrumbs();
    }

    private async Task NavigateAsync(string? path, bool remember = true, double offset = 0)
    {
        var context = Store.CurrentScanContext;
        if (path != null && !context.Scope.Allows(path)) return;
        if (remember) _history.Push((_path, ExplorerScroll.VerticalOffset));
        _load?.Cancel(); _load?.Dispose();
        var load = _load = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        _path = path; _selected = null; _limit = 50; _bucket = 0;
        _busy = true; _entries = []; FolderSearch.Text = "";
        RefreshEntries(); BuildBreadcrumbs(); UpdateBusy();
        try
        {
            var entries = path == null
                ? await EverythingElseScanner.MeasureAsync(Store.DiskUsed, 0, Store.TrashBytes, context.Scope.Roots, load.Token, context.Scope.Excluded)
                : await FolderExplorer.ReadAsync(path, context.Scope, load.Token);
            if (load.IsCancellationRequested || !ReferenceEquals(context, Store.CurrentScanContext)) return;
            SetEntries(entries);
            // Recalculate the newly restored list before clamping its scroll offset.
            ExplorerScroll.UpdateLayout();
            ExplorerScroll.ChangeView(null, offset, null, true);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!load.IsCancellationRequested)
            {
                FolderStatus.Text = Loc.T("Dossier inaccessible. Revenez en arrière ou actualisez.", "Folder unavailable. Go back or refresh.");
                App.LogCrash("Folder explorer", ex);
            }
        }
        finally
        {
            if (ReferenceEquals(load, _load))
            {
                _busy = false; UpdateBusy();
                EmptyText.Visibility = _visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void SetEntries(IEnumerable<EverythingElseFolder> entries)
    {
        _entries = entries.ToList();
        _selected = _entries.FirstOrDefault(i => i.IsDirectory && i.Bytes > 0) ?? _entries.FirstOrDefault();
        _limit = 50;
        FolderStatus.Text = Loc.T("Lecture seule · ≈ indique une taille estimée. Explorez un dossier pour affiner.",
            "Read only · ≈ marks an estimated size. Explore a folder to refine it.");
        if (_entries.Count >= 2000) FolderStatus.Text += Loc.T(" · 2 000 premiers éléments.", " · First 2,000 items.");
        RefreshEntries();
    }

    private void SyncTexts()
    {
        _syncing = true;
        BackBtn.Content = Loc.T("← Vue d'ensemble", "← Overview");
        PageTitle.Text = Loc.T("Explorer l'espace disque", "Explore disk space");
        PageSubtitle.Text = Loc.T("Carte, recherche et navigation dans les dossiers. Aucune suppression ici.",
            "Map, search and folder navigation. Nothing is deleted here.");
        RefreshBtn.Content = Loc.T("Actualiser", "Refresh");
        Bucket1Title.Text = Loc.T("Géré dans les apps", "Managed in apps");
        Bucket2Title.Text = Loc.T("Vos fichiers", "Your files");
        Bucket3Title.Text = Loc.T("Profils et données d'apps", "App profiles and data");
        Bucket4Title.Text = Loc.T("Géré par Windows", "Managed by Windows");
        BucketPicker.ItemsSource = new[] { Loc.T("Tout", "All"), Bucket1Title.Text, Bucket2Title.Text,
            Bucket3Title.Text, Bucket4Title.Text, Loc.T("Non détaillé", "Not detailed") };
        BucketPicker.SelectedIndex = _bucket;
        SortPicker.ItemsSource = new[] { Loc.T("Taille ↓", "Size ↓"), Loc.T("Nom A–Z", "Name A–Z") };
        SortPicker.SelectedIndex = _sort;
        MapToggle.Header = Loc.T("Carte proportionnelle", "Proportional map");
        MapToggle.OnContent = Loc.T("Visible", "Shown"); MapToggle.OffContent = Loc.T("Masquée", "Hidden");
        FolderSearch.PlaceholderText = Loc.T("Rechercher un dossier ou un chemin", "Search folders or paths");
        MoreButton.Content = Loc.T("Afficher 50 éléments de plus", "Show 50 more items");
        ExploreBtn.Content = Loc.T("Explorer le dossier", "Explore folder");
        RevealBtn.Content = Loc.T("Afficher dans Windows", "Show in Windows");
        EmptyText.Text = Loc.T("Aucun élément à afficher. Modifiez le filtre ou actualisez l'analyse.", "No items to show. Change the filter or refresh the scan.");
        _syncing = false;
        RefreshEntries(); BuildBreadcrumbs();
    }

    private void UpdateBusy()
    {
        FolderBusy.Visibility = _busy ? Visibility.Visible : Visibility.Collapsed; FolderBusy.IsActive = _busy;
        RefreshBtn.IsEnabled = !_busy && Store.HasActiveDrives; RefreshBtn.IsBusy = _busy;
        if (_busy) FolderStatus.Text = Loc.T("Lecture des dossiers… Vous pouvez toujours faire défiler ou revenir en arrière.",
            "Reading folders… You can still scroll or go back.");
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => Store.OverviewPage = OverviewSubPage.Summary;
    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await NavigateAsync(_path, false);
    public Task RefreshCurrentFolderAsync() => NavigateAsync(_path, false);
    private async void OnParentClick(object sender, RoutedEventArgs e)
    {
        if (_history.TryPop(out var previous)) await NavigateAsync(previous.Path, false, previous.Offset);
        else if (_path != null) await NavigateAsync(null, false);
    }
}
