using Microsoft.UI.Xaml.Controls;
using PurgeWin.Utilities;

namespace PurgeWin.Views;

public sealed partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        Loc.LanguageChanged += SyncTexts;
        Loaded += (_, _) => SyncTexts();
    }

    private void SyncTexts()
    {
        TxtVersion.Text = Loc.T("Version 1.0.1 pour Windows", "Version 1.0.1 for Windows");
        TxtTagline.Text = Loc.T("Libérez votre PC. En toute sécurité.", "Declutter your PC. Safely.");
        TxtCard1Title.Text = Loc.T("Corbeille par défaut", "Recycle Bin by Default");
        TxtCard1Body.Text = Loc.T(
            "Les fichiers supprimés sont toujours envoyés dans la Corbeille de Windows et ne sont jamais effacés définitivement sans votre accord.",
            "Cleaned files are always moved to the Windows Recycle Bin first and never permanently erased behind your back.");
        TxtCard2Title.Text = Loc.T("Liste autorisée stricte (Allowlist)", "Strict Allowlist Policy");
        TxtCard2Body.Text = Loc.T(
            "Seuls les répertoires et caches répertoriés dans le catalogue officiel sont nettoyés automatiquement. Rien d'inconnu n'est touché.",
            "Only caches listed in the verified allowlist catalog are eligible for safe cleaning. Unknown paths are never touched.");
        TxtCard3Title.Text = Loc.T("Support multi-disques & Confidentialité totale", "Multi-Drive Support & 100% Offline Privacy");
        TxtCard3Body.Text = Loc.T(
            "Analyse tous vos disques locaux (C:, D:, E:...) entièrement hors-ligne. Aucune donnée ni télémétrie n'est envoyée sur Internet.",
            "Scans all your local drives (C:, D:, E:...) completely offline. Zero network calls or telemetry.");
    }
}
