using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using PurgeWin.Models;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Controls;

public sealed partial class DriveScopeBar : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;
    private bool _syncing;
    private int _catalog = -1;

    public DriveScopeBar()
    {
        InitializeComponent();
        Loaded += (_, _) => { Store.PropertyChanged += OnStoreChanged; Loc.LanguageChanged += Sync; Sync(); };
        Unloaded += (_, _) => { Store.PropertyChanged -= OnStoreChanged; Loc.LanguageChanged -= Sync; };
    }

    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PurgeStore.ScopeStatus) or nameof(PurgeStore.ScopeTitle)
            or nameof(PurgeStore.DriveCatalogVersion) or nameof(PurgeStore.IsCleaning)
            or nameof(PurgeStore.IsScanning) or nameof(PurgeStore.SelectedTab)) Sync();
    }

    private void Sync()
    {
        _syncing = true;
        try
        {
            Visibility = Store.SelectedTab is AppTab.Overview or AppTab.About or AppTab.Settings ? Visibility.Collapsed : Visibility.Visible;
            if (_catalog != Store.DriveCatalogVersion)
            {
                DrivePicker.ItemsSource = Store.Drives.ToArray();
                _catalog = Store.DriveCatalogVersion;
            }
            DrivePicker.SelectedItem = Store.Drives.FirstOrDefault(d => d.Key == Store.Settings.SelectedDrive);
            DrivePicker.IsEnabled = !Store.IsCleaning;
            RefreshButton.IsEnabled = !Store.IsCleaning && !Store.IsScanning;
            ScopeLabel.Text = Loc.T("Afficher", "Show");
            ScopeStatus.Text = Store.ScopeStatus;
            StopButton.Content = Loc.T("Arrêter", "Stop");
            StopButton.Visibility = Store.IsScanning ? Visibility.Visible : Visibility.Collapsed;
            ScanRing.Visibility = Store.IsScanning ? Visibility.Visible : Visibility.Collapsed;
            ScanRing.IsActive = Store.IsScanning;
            AutomationProperties.SetName(DrivePicker, Loc.T("Disque affiché dans toutes les analyses", "Drive shown in all scans"));
            AutomationProperties.SetName(RefreshButton, Loc.T("Actualiser les disques et analyser ce périmètre", "Refresh drives and scan this scope"));
            ToolTipService.SetToolTip(RefreshButton, Loc.T("Actualiser les disques et les résultats", "Refresh drives and results"));
        }
        finally { _syncing = false; }
    }

    private void OnDriveChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && DrivePicker.SelectedItem is DriveVolumeItem drive) Store.SetDrive(drive.Key);
    }
    private async void OnRefresh(object sender, RoutedEventArgs e) => await Store.RefreshCurrentScopeAsync();
    private void OnStop(object sender, RoutedEventArgs e) => Store.CancelScan();
}
