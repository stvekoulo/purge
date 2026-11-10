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
            var outcome = store.LastCleanOutcome;
            var freed = outcome.FreedBytes;
            TxtFreedBytes.Text = ByteFormatter.Format(freed);
            var count = outcome.MovedCount;
            TxtItemCount.Text = Loc.T(
                $"{count} élément{(count > 1 ? "s" : "")} nettoyé{(count > 1 ? "s" : "")}",
                $"{count} item{(count > 1 ? "s" : "")} cleaned");
            if (count == 0 && outcome.Failures.Count > 0)
            {
                TxtSubtitle.Text = Loc.T("aucun élément déplacé", "no items moved");
            }

            if (outcome.Failures.Count > 0)
            {
                var first = outcome.Failures[0];
                var firstName = Path.GetFileName(first.Path.TrimEnd('\\'));
                if (string.IsNullOrEmpty(firstName)) firstName = first.Path;
                var skipCount = outcome.Failures.Count;
                SkippedBanner.Visibility = Visibility.Visible;
                TxtSkippedSummary.Text = Loc.T(
                    $"{skipCount} élément{(skipCount > 1 ? "s" : "")} non nettoyé{(skipCount > 1 ? "s" : "")} ({firstName} : {first.Reason})",
                    $"{skipCount} item{(skipCount > 1 ? "s" : "")} skipped ({firstName}: {first.Reason})");
            }
            else
            {
                SkippedBanner.Visibility = Visibility.Collapsed;
            }

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
