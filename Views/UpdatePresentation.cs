using PurgeWin.Services;
using PurgeWin.Utilities;
using PurgeWin.ViewModels;

namespace PurgeWin.Views;

internal sealed record UpdatePresentation(string Title, string Detail, string Action, string Glyph)
{
    public static UpdatePresentation From(UpdateState state)
    {
        string version = state.Info?.LatestVersion ?? "";
        if (state.Error == UpdateError.Install)
            return new(Loc.T("Installation non démarrée", "Installation did not start"),
                Loc.T("Réessayez lorsque vous êtes prêt. Purge reste ouvert.", "Try again when you are ready. Purge stays open."),
                Loc.T("Redémarrer pour mettre à jour", "Restart to update"), "\uE7BA");
        return state.Stage switch
        {
            UpdateStage.Checking => new(Loc.T("Recherche de mises à jour", "Checking for updates"),
                Loc.T("Vérification de la dernière version…", "Checking the latest version…"),
                Loc.T("Vérification…", "Checking…"), "\uE895"),
            UpdateStage.UpToDate => new(Loc.T("Purge est à jour", "Purge is up to date"),
                Loc.T($"Vous utilisez la version {UpdateService.CurrentVersion}.", $"You are using version {UpdateService.CurrentVersion}."), "", "\uE73E"),
            UpdateStage.Available => new(Loc.T($"Mise à jour {version} disponible", $"Update {version} available"),
                Loc.T("Téléchargez la mise à jour ici. Vous choisirez quand redémarrer.", "Download the update here. Choose when to restart."),
                Loc.T("Télécharger", "Download"), "\uE896"),
            UpdateStage.Downloading => new(Loc.T($"Téléchargement de Purge {version}", $"Downloading Purge {version}"),
                $"{state.Percent:0} % · {ByteFormatter.Format(state.DownloadedBytes)} / {ByteFormatter.Format(state.TotalBytes)}",
                Loc.T("Téléchargement…", "Downloading…"), "\uE896"),
            UpdateStage.Verifying => new(Loc.T("Vérification du téléchargement", "Verifying download"),
                Loc.T("Vérification de l'intégrité du fichier…", "Checking the file's integrity…"),
                Loc.T("Vérification…", "Verifying…"), "\uE895"),
            UpdateStage.Ready => new(Loc.T($"Purge {version} est prêt à être installé", $"Purge {version} is ready to install"),
                PurgeStore.Shared.IsCleaning
                    ? Loc.T("Attendez la fin du nettoyage pour redémarrer.", "Wait for cleaning to finish before restarting.")
                    : Loc.T("Le téléchargement est vérifié. Vos réglages seront conservés.", "The download is verified. Your settings will be kept."),
                Loc.T("Redémarrer pour mettre à jour", "Restart to update"), "\uE73E"),
            UpdateStage.Installing => new(Loc.T("Redémarrage pour la mise à jour", "Restarting to update"),
                Loc.T("Purge va se fermer puis se rouvrir après l'installation.", "Purge will close and reopen after installation."),
                Loc.T("Redémarrage…", "Restarting…"), "\uE777"),
            UpdateStage.Error => new(Loc.T("Mise à jour interrompue", "Update interrupted"),
                ErrorText(state.Error), Loc.T("Réessayer", "Retry"), "\uE7BA"),
            _ => new(Loc.T("Mises à jour du logiciel", "Software updates"),
                Loc.T("Recherchez une nouvelle version de Purge.", "Check for a new version of Purge."), "", "\uE895")
        };
    }

    public static bool CanAct(UpdateState state) => !state.IsBusy
        && !(state.Stage == UpdateStage.Ready && PurgeStore.Shared.IsCleaning);

    public static async Task ActAsync()
    {
        var updater = UpdateService.Shared;
        var state = updater.State;
        if (!CanAct(state)) return;
        if (state.Stage == UpdateStage.Ready)
        {
            if (await updater.RestartToInstallAsync(AppContext.BaseDirectory, Environment.ProcessId,
                () => !PurgeStore.Shared.IsCleaning)) Microsoft.UI.Xaml.Application.Current.Exit();
        }
        else if (state.Info?.Asset != null) await updater.DownloadAsync();
        else await updater.CheckAsync();
    }

    private static string ErrorText(UpdateError? error) => error switch
    {
        UpdateError.Integrity => Loc.T("Le fichier n'a pas pu être vérifié. Téléchargez-le à nouveau.", "The file could not be verified. Download it again."),
        UpdateError.NoInstaller => Loc.T("Cette version n'a pas encore d'installateur compatible. Réessayez plus tard.", "This version has no compatible installer yet. Try again later."),
        UpdateError.Download => Loc.T("Téléchargement impossible. Vérifiez la connexion et l'espace disque, puis réessayez.", "Download failed. Check your connection and free disk space, then retry."),
        _ => Loc.T("Vérification impossible. Vérifiez votre connexion puis réessayez.", "Could not check for updates. Check your connection and try again.")
    };
}
