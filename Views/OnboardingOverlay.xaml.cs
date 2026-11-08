using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

/// Purge for Windows: Welcome -> First Scan -> Results with
/// SizeComparisonCatalog anchor and choice between Clean Safe Now or Review First.
public sealed partial class OnboardingOverlay : UserControl
{
    private PurgeStore Store => PurgeStore.Shared;

    public OnboardingOverlay()
    {
        InitializeComponent();
        Loc.LanguageChanged += SyncTexts;
        Store.PropertyChanged += (_, _) => SyncResults();
        Loaded += (_, _) => SyncTexts();
    }

    public void ShowWelcome()
    {
        StepWelcome.Visibility = Visibility.Visible;
        StepResults.Visibility = Visibility.Collapsed;
        Visibility = Visibility.Visible;
        SyncTexts();
    }

    private void SyncTexts()
    {
        TxtWelcomeTitle.Text = Loc.T("Bienvenue dans Purge", "Welcome to Purge");
        TxtWelcomeSub.Text = Loc.T(
            "Libérez de l'espace sur vos disques en toute sécurité.",
            "Declutter your drives safely and transparently.");
        TxtPillar1Title.Text = Loc.T("✓ Corbeille Windows par défaut", "✓ Recycle Bin by default");
        TxtPillar1Sub.Text = Loc.T(
            "Tout ce que Purge nettoie va dans la Corbeille : rien n'est effacé définitivement dans votre dos.",
            "Everything Purge cleans goes to the Recycle Bin first: nothing is permanently deleted behind your back.");
        TxtPillar2Title.Text = Loc.T("✓ Liste blanche vérifiée & Multi-disques", "✓ Strict Allowlist & Multi-Drive Scan");
        TxtPillar2Sub.Text = Loc.T(
            "Analyse vos disques (C:, D:, E:...) 100 % hors-ligne sans toucher à vos mots de passe ou sessions.",
            "Scans your drives (C:, D:, E:...) 100% offline without touching your passwords or logins.");
        BtnStartScan.Content = Loc.T("Voir les résultats de l'analyse →", "See Scan Results →");

        TxtResultsKicker.Text = Loc.T("ESPACE RÉCUPÉRABLE DÉTECTÉ", "RECLAIMABLE SPACE FOUND");
        BtnReviewFirst.Content = Loc.T("Tout examiner d'abord", "Review everything first");
        SyncResults();
    }

    private void SyncResults()
    {
        long total = Store.TotalReclaimableBytes;
        long safe = Store.SafeBytes;
        TxtResultsBytes.Text = ByteFormatter.Format(total > 0 ? total : safe);

        var cmp = SizeComparisonCatalog.Describe(total > 0 ? total : safe);
        if (cmp is { } c)
        {
            ComparisonChip.Visibility = Visibility.Visible;
            TxtComparison.Text = c.Label;
        }
        else
        {
            ComparisonChip.Visibility = Visibility.Collapsed;
        }

        TxtSafeLine.Text = Loc.T(
            $"{ByteFormatter.Format(safe)} sûrs à nettoyer immédiatement ({Store.SafeCount} éléments)",
            $"{ByteFormatter.Format(safe)} safe to clean right now ({Store.SafeCount} items)");
        TxtReviewLine.Text = Loc.T(
            $"Plus {ByteFormatter.Format(Math.Max(0, total - safe))} dans les projets de dev, fichiers volumineux et restes d'apps.",
            $"Plus {ByteFormatter.Format(Math.Max(0, total - safe))} across dev projects, large files, and app leftovers.");

        BtnCleanSafeNow.Content = safe > 0
            ? Loc.T($"Envoyer {ByteFormatter.Format(safe)} à la Corbeille", $"Move {ByteFormatter.Format(safe)} to Recycle Bin")
            : Loc.T("Continuer", "Continue");
    }

    private void OnStartScanClick(object sender, RoutedEventArgs e)
    {
        StepWelcome.Visibility = Visibility.Collapsed;
        StepResults.Visibility = Visibility.Visible;
        SyncResults();
    }

    private async void OnCleanSafeNowClick(object sender, RoutedEventArgs e)
    {
        CompleteOnboarding();
        if (Store.SafeBytes > 0)
            await Store.CleanSafeItemsAsync();
    }

    private void OnReviewFirstClick(object sender, RoutedEventArgs e)
    {
        CompleteOnboarding();
        Store.SelectedTab = AppTab.Overview;
    }

    private void CompleteOnboarding()
    {
        Store.Settings.HasCompletedOnboarding = true;
        Store.SaveSettings();
        Visibility = Visibility.Collapsed;
    }
}
