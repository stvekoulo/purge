using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PurgeWin.Models;
using PurgeWin.Services;
using PurgeWin.Utilities;

namespace PurgeWin.ViewModels;

public sealed partial class PurgeStore
{
    public ObservableCollection<StartupAppItem> StartupItems { get; } = new BulkObservableCollection<StartupAppItem>();
    public ObservableCollection<StartupAppItem> FilteredStartupItems { get; } = [];
    public ObservableCollection<MaintenanceTaskItem> MaintenanceTasks { get; } =
        new(MaintenanceRunner.CreateDefaultTasks());

    [ObservableProperty] private ScanPhase _optimizationPhase;
    [ObservableProperty] private OptimizationSubTab _optimizationSubTab = OptimizationSubTab.StartupApps;
    [ObservableProperty] private string _startupSearchQuery = "";

    public int EnabledStartupCount => StartupItems.Count(i => i.IsEnabled);
    public int DisabledStartupCount => StartupItems.Count(i => !i.IsEnabled);
    public int SelectedMaintenanceCount => MaintenanceTasks.Count(t => t.IsSelected);

    public string OptimizationSubtitle => StartupItems.Count == 0 && OptimizationPhase != ScanPhase.Done
        ? EmptyScanText(OptimizationPhase)
        : Loc.T(
            $"{EnabledStartupCount} active{(EnabledStartupCount > 1 ? "s" : "")} au démarrage · {DisabledStartupCount} désactivée{(DisabledStartupCount > 1 ? "s" : "")} · {MaintenanceTasks.Count} outils système",
            $"{EnabledStartupCount} enabled at login · {DisabledStartupCount} disabled · {MaintenanceTasks.Count} system tasks");

    public Task ScanOptimizationAsync() => RunScanAsync(
        Loc.T("Optimisation & démarrage", "Optimization & startup"),
        p => OptimizationPhase = p,
        c => StartupManagerService.ScanAsync(c.Token),
        items =>
        {
            Replace(StartupItems, items, item => item.PropertyChanged += (_, _) => NotifyOptimizationChanged());
            ApplyOptimizationFilter();
            NotifyOptimizationChanged();
        });

    public void ApplyOptimizationFilter()
    {
        var q = StartupSearchQuery.Trim();
        var items = StartupItems.AsEnumerable();
        if (!string.IsNullOrEmpty(q))
        {
            items = items.Where(i =>
                i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Command.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        FilteredStartupItems.Clear();
        foreach (var item in items) FilteredStartupItems.Add(item);
        NotifyOptimizationChanged();
    }

    public bool ToggleStartupApp(StartupAppItem item, bool enabled)
    {
        if (item.IsEnabled == enabled) return true;
        var ok = StartupManagerService.SetStartupEnabled(item, enabled);
        NotifyOptimizationChanged();
        return ok;
    }

    public async Task RunSingleMaintenanceAsync(MaintenanceTaskItem task)
    {
        if (IsCleaning) return;
        IsCleaning = true;
        CleaningLabel = task.Title;
        NotifyScope();
        try
        {
            var (freed, _) = await MaintenanceRunner.RunTaskAsync(task);
            if (freed > 0)
                RecordOutcome(new CleanOutcome(freed, 1, [task.Title], []), "optimization");
            RefreshDisk();
        }
        finally
        {
            IsCleaning = false;
            CleaningLabel = "";
            NotifyScope();
            NotifyOptimizationChanged();
        }
    }

    public async Task RunSelectedMaintenanceAsync()
    {
        var picked = MaintenanceTasks.Where(t => t.IsSelected).ToList();
        if (IsCleaning || picked.Count == 0) return;

        IsCleaning = true;
        NotifyScope();
        long totalFreed = 0;
        int okCount = 0;
        var completed = new List<string>();
        try
        {
            for (int i = 0; i < picked.Count; i++)
            {
                var task = picked[i];
                CleaningLabel = $"{task.Title} ({i + 1}/{picked.Count})…";
                var (freed, ok) = await MaintenanceRunner.RunTaskAsync(task);
                totalFreed += freed;
                if (ok) { okCount++; completed.Add(task.Title); }
            }
            if (totalFreed > 0)
                RecordOutcome(new CleanOutcome(totalFreed, okCount, completed, []), "optimization");
            RefreshDisk();
        }
        finally
        {
            IsCleaning = false;
            CleaningLabel = "";
            NotifyScope();
            NotifyOptimizationChanged();
        }
    }

    public void RefreshOptimizationLocalization()
    {
        foreach (var item in StartupItems) item.RefreshLocalizedText();
        foreach (var task in MaintenanceTasks) task.RefreshLocalizedText();
        NotifyOptimizationChanged();
    }

    public void NotifyOptimizationChanged()
    {
        OnPropertyChanged(nameof(EnabledStartupCount));
        OnPropertyChanged(nameof(DisabledStartupCount));
        OnPropertyChanged(nameof(SelectedMaintenanceCount));
        OnPropertyChanged(nameof(OptimizationSubtitle));
    }
}
