<p align="center">
  <img src="./Assets/readme/hero.svg" width="100%" alt="Purge — Libérez votre PC Windows en toute sécurité. Nettoyeur natif WinUI 3 multi-disques pour caches, outils de dev et fichiers volumineux.">
</p>

<p align="center">
  <a href="./README.md">English</a> · <strong>Français</strong> · <a href="https://github.com/stvekoulo/purge/releases/latest"><strong>⬇ Télécharger la dernière version (v1.0.0)</strong></a>
</p>

<p align="center">
  <a href="https://github.com/stvekoulo/purge/releases/latest"><img src="https://img.shields.io/github/v/release/stvekoulo/purge?style=flat-square&label=T%C3%A9l%C3%A9charger&color=8A6CF6" alt="Télécharger la dernière version"></a>
  <img src="https://img.shields.io/badge/Plateforme-Windows%2010%20%2F%2011%20(x64%20%26%20ARM64)-6B4DE6?style=flat-square" alt="Plateforme : Windows 10/11">
  <img src="https://img.shields.io/badge/Interface-WinUI%203%20%C2%B7%20.NET%2010-2E2D2B?style=flat-square" alt="Interface : WinUI 3 .NET 10">
  <img src="https://img.shields.io/badge/S%C3%A9curit%C3%A9-Corbeille%20par%20d%C3%A9faut-1F7A35?style=flat-square" alt="Sécurité : Corbeille par défaut">
  <img src="https://img.shields.io/badge/Confidentialit%C3%A9-100%25%20Hors--ligne-2E2D2B?style=flat-square" alt="Confidentialité : 100% Hors-ligne">
  <a href="./LICENSE"><img src="https://img.shields.io/badge/Licence-MIT-ECEAE6?style=flat-square" alt="Licence : MIT"></a>
</p>

---

## Aperçu réel de l'application

<p align="center">
  <img src="./Assets/readme/screenshot-overview-fr.png" width="100%" alt="Vue d'ensemble de Purge en français (mode sombre) avec analyse multi-disques">
</p>

<p align="center">
  <img src="./Assets/readme/screenshot-overview-light.png" width="49%" alt="Purge en mode clair">
  <img src="./Assets/readme/screenshot-devtools-dark.png" width="49%" alt="Outils de développement dans Purge">
</p>

<p align="center">
  <img src="./Assets/readme/screenshot-treemap-dark.png" width="49%" alt="Carte Treemap Tout le reste dans Purge">
  <img src="./Assets/readme/screenshot-settings-dark.png" width="49%" alt="Réglages de Purge avec thèmes Clair, Sombre, Système et choix de langue">
</p>

---

## Qu'est-ce que Purge ?

**Purge** est un analyseur d'espace disque, nettoyeur de caches système et nettoyeur d'artefacts de développement natif **WinUI 3** pour **Windows 11 et Windows 10**. Il inspecte tous vos disques locaux (`C:`, `D:`, `E:`, ou l'ensemble des disques combinés) et répartit l'espace récupérable en cinq catégories claires — accompagnées d'une carte proportionnelle (Treemap) de tout le reste de votre PC.

Chaque élément est documenté en langage clair pour expliquer son rôle et l'effet de son nettoyage. Rien n'est jamais effacé définitivement dans votre dos : **tous les éléments nettoyés sont envoyés dans la Corbeille de Windows**.

---

## Pourquoi Purge est différent

<p align="center">
  <img src="./Assets/readme/architecture.svg" width="100%" alt="Architecture de sécurité de Purge en 3 étapes">
</p>

| Principe | Garantie technique |
| :--- | :--- |
| **Corbeille par défaut** | Utilise l'API native Windows Shell `SHFileOperationW` (`FOF_ALLOWUNDO`) afin que chaque fichier ou dossier nettoyé puisse être restauré depuis la Corbeille. |
| **Liste blanche stricte (Allowlist)** | Seuls les emplacements vérifiés dans [`windows-cache-catalog.json`](./Resources/windows-cache-catalog.json) sont éligibles au nettoyage sûr en un clic. Les mots de passe, sessions, cookies et clés SSH/GPG ne sont jamais touchés. |
| **Analyse multi-disques** | Analysez tous vos disques (`Tous les disques`) simultanément ou filtrez disque par disque (`C:`, `D:`, `E:`). |
| **Conçu pour les développeurs** | Détecte les caches globaux (`npm`, `pnpm`, `Yarn`, `Bun`, `NuGet`, `Cargo`, `pip`, `uv`, `Gradle`, `Maven`, `Go`, `JetBrains`, `VS Code`, `Cursor`) et les dossiers de build (`node_modules`, `target`, `.next`, `dist`, `bin/obj`, `.venv`, émulateurs Android AVD et worktrees IA). |
| **Respect de OneDrive & des liens NTFS** | Ne traverse jamais les jonctions ou liens symboliques NTFS et permet de libérer l'espace local des fichiers OneDrive (`attrib +U -P`) tout en les conservant dans le cloud. |
| **100 % hors-ligne & bilingue** | Zéro télémétrie, bascule instantanée **Français / English**, et thèmes **Clair / Sombre / Système**. |

---

## Télécharger ou compiler depuis les sources

### Option 1 — Version portable prête à l'emploi

1. Téléchargez **`Purge-v1.0.0-win-x64.zip`** depuis la page [**GitHub Releases**](https://github.com/stvekoulo/purge/releases/latest).
2. Décompressez l'archive et lancez **`Purge.exe`**.

### Option 2 — Compiler depuis les sources

```powershell
# Cloner le dépôt
git clone https://github.com/stvekoulo/purge.git
cd purge

# Compiler le projet (x64)
dotnet build PurgeWin.csproj -p:Platform=x64

# Lancer Purge
dotnet run --project PurgeWin.csproj -p:Platform=x64
```

---

## Licence

Distribué sous [Licence MIT](./LICENSE).
