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
                UpdateVisibility();
        };
        Loc.LanguageChanged += UpdateVisibility;
    }

    private void UpdateVisibility()
    {
        var store = PurgeStore.Shared;
        TxtSubtitle.Text = Loc.T("libérés et placés dans la Corbeille", "freed and moved to the Recycle Bin");
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
