using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

public sealed partial class CelebrationOverlay : UserControl
{
    public CelebrationOverlay()
    {
        InitializeComponent();
        PurgeStore.Shared.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PurgeStore.ShowCelebration))
                SafeDispatch(UpdateVisibility);
        };
        Loc.LanguageChanged += () => SafeDispatch(UpdateVisibility);
    }

    private void SafeDispatch(Action action)
    {
        if (DispatcherQueue is { } q)
        {
            if (q.HasThreadAccess) action();
            else q.TryEnqueue(() => action());
        }
    }

    private void UpdateVisibility()
    {
        var store = PurgeStore.Shared;
        TxtSubtitle.Text = store.LastCleanOutcome?.RecycleBinEmptied == true
            ? Loc.T("traités · Corbeille vidée définitivement", "processed · Recycle Bin permanently emptied")
            : store.LastCleanOutcome?.RecycleBinEmptyRequested == true
                ? Loc.T("traités · Vérifiez le résultat du vidage", "processed · Check the Recycle Bin result")
            : Loc.T("déplacés dans la Corbeille (espace non encore libéré)", "moved to Recycle Bin (space not yet freed)");
        BtnDone.Content = Loc.T("Terminé", "Done");
        TxtTagline.Text = SizeComparisonCatalog.TimeTagline();

        if (store.ShowCelebration && store.LastCleanOutcome is not null)
        {
            var freed = store.LastCleanOutcome.FreedBytes;
            TxtFreedBytes.Text = ByteFormatter.Format(freed);
            var count = store.LastCleanOutcome.MovedCount;
            TxtItemCount.Text = Loc.T(
                $"{count} élément{(count > 1 ? "s" : "")} nettoyé{(count > 1 ? "s" : "")}",
                $"{count} item{(count > 1 ? "s" : "")} cleaned");

            if (SizeComparisonCatalog.Describe(freed) is { } cmp)
            {
                ComparisonChip.Visibility = Visibility.Visible;
                TxtComparison.Text = cmp.Label;
            }
            else
            {
                ComparisonChip.Visibility = Visibility.Collapsed;
            }
            Visibility = Visibility.Visible;
        }
        else
        {
            Visibility = Visibility.Collapsed;
        }
    }

    private void BtnDone_Click(object sender, RoutedEventArgs e) =>
        PurgeStore.Shared.DismissCelebration();
}
