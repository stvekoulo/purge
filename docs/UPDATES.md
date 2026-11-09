# Mises à jour intégrées — 1.0.4

La version utilisée par la détection et la page « À propos » provient désormais
du binaire. Les formes `1.0.3` et `1.0.3.0` sont équivalentes ; une version
identique ou antérieure ne déclenche pas de proposition de mise à jour.

Le parcours est partagé entre la bannière et la page « À propos » :

1. Télécharger dans Purge, avec pourcentage et quantité reçue.
2. Annuler ou réessayer si nécessaire.
3. Vérifier la taille et l'empreinte SHA-256 fournie par GitHub.
4. Proposer « Redémarrer pour mettre à jour », au choix de l'utilisateur.
5. Attendre la fermeture du processus, installer au même emplacement et rouvrir
   Purge. Le mode d'installation existant, les réglages et l'historique restent
   conservés. Le nettoyage en cours bloque l'action de redémarrage.

Un téléchargement vérifié est retrouvé à la prochaine ouverture. Il est
revérifié avant toute exécution. Aucun lien de téléchargement n'ouvre le
navigateur. Une erreur de lancement de l'installateur laisse Purge ouvert.

## Validation du 9 novembre 2026

- Deux tests de version échouaient avec le service d'origine ; ils passent
  après correction. La suite complète comporte 37 contrôles réussis.
- Tests HTTP simulés : versions identiques/anciennes, architecture absente,
  préversion, URL non autorisée, checksum invalide, concurrence, progression,
  cache, annulation, corruption, nouvelle tentative, perte réseau et refus
  de lancement de l'installateur.
- Téléchargement réel de l'installateur public 1.0.3 : 75 562 410 octets,
  empreinte SHA-256 validée, aucune exécution du fichier téléchargé.
- Test Inno Setup isolé : attente de sortie d'un processus, copie et relance
  d'une application témoin ; aucune entrée de désinstallation ni modification
  de Purge installé.
- Interface native : progression synchronisée, bouton de redémarrage, retour
  après échec simulé et version affichée contrôlés sur Windows x64.
- Publication locale x64 et compilation de l'installateur 1.0.4 réussies.
  ARM64/x86, UAC administratif réel et CI distante non exécutés dans cette tâche.

```powershell
dotnet run --project Tests/UpdateTests.csproj
# Après compilation Tests/UpdateHandoff.Test.iss avec Inno Setup :
.\scripts\Test-UpdateHandoff.ps1
```

Le paramètre MSBuild `UpdatePreview=true` inclut une simulation réservée aux
contrôles visuels : une fausse 1.0.5 et un téléchargement inerte. Elle ne peut
jamais installer de logiciel et n'est pas incluse dans la publication normale.

Le nouvel installateur local doit être installé une première fois pour
remplacer la 1.0.3 : celle-ci ne contient pas encore ce parcours intégré.
Aucune release publique n'a été créée dans cette tâche.
