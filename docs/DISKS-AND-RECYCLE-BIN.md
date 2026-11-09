# Disques, exploration et Corbeille — 1.0.5

Le sélecteur global « Tous les disques / disque précis » s'applique aux analyses,
compteurs, résultats, actions de nettoyage et à la Corbeille. Le choix est conservé.
Changer de disque annule les anciennes analyses, efface les sélections et réutilise
les résultats déjà disponibles. Une fin d'analyse tardive ne peut pas remplacer
le périmètre courant. Un disque déconnecté ne devient jamais implicitement « Tous ».
Les exclusions sont également vérifiées avant déplacement vers la Corbeille.

La vue d'exploration propose un défilement unique, une liste accessible même pour
les petites tuiles, une recherche locale, un tri, des catégories, des boutons
d'entrée dans les dossiers, un fil de navigation et un retour à la position précédente.
Elle reste en lecture seule. Les mesures rapides de dossiers peuvent être partielles :
« ≈ » indique une estimation ; l'espace non mesuré reste explicitement non détaillé,
sans être arbitrairement attribué à Windows. Limites : 2 000 enfants par dossier,
50 lignes supplémentaires par clic, échantillonnage borné pour garder la navigation réactive.

« Vider la Corbeille après nettoyage » est désactivé par défaut. L'option figure
dans Réglages, la confirmation de nettoyage et le menu Corbeille. Après un nettoyage
ayant déplacé des éléments, elle vide définitivement toute la Corbeille des volumes
concernés, anciens éléments compris. Le texte et l'historique distinguent ce cas
d'un déplacement restaurable. Le nettoyage planifié respecte également cette option.
Le menu Corbeille permet d'ouvrir Windows, demander une restauration du périmètre,
choisir individuellement dans Windows ou confirmer un vidage définitif.

Les versions de l'exécutable et de l'assembly sont dérivées de la version du projet :
les anciennes valeurs explicites 1.0.4.0 ont été supprimées. Le contrôle de démarrage
des scripts local et CI vérifie aussi la version attendue avant lancement.

## Validation locale du 9 novembre 2026

- 65 tests automatiques réussis : mise à jour, chemins, exclusions, annulation,
  exploration, collections et simulation du vidage limité aux volumes sélectionnés.
- 9 contrôles d'intégration réussis sur les vrais scans « Tous », C: et E: :
  résultats cloisonnés, sélections effacées, ancienne analyse rejetée, cache réutilisé,
  vidage automatique désactivé par défaut.
- Interface native x64 : changement de disque, compteurs, défilement à la molette
  au-dessus de la carte, liste, entrée dans un dossier, recherche et retour à la
  position précédente (975 px après correction du recalcul de disposition), menu Corbeille et
  confirmation de vidage inspectée puis annulée, réglage initial décoché.
- Deux fichiers jetables créés uniquement pour le test, dont un de zéro octet,
  déplacés vers la Corbeille puis restaurés avec vérification du contenu.
  La restauration du test ciblait leurs chemins exacts.
- Publication x64 et compilation Inno Setup terminées ; exécutable et assembly
  vérifiés en 1.0.5.0 ; démarrage du binaire final avec fenêtre réactive réussi.

Aucun nettoyage ni vidage de Corbeille de l'utilisateur n'a été exécuté.
Le vidage réel reste non testé de bout en bout ; son appel natif est couvert par
simulation. Les architectures ARM64/x86, les conflits de restauration Windows,
les thèmes/langues et toutes les tailles de fenêtre ne sont pas intégralement
validés. Les avertissements MVVM Toolkit préexistants subsistent.
L'installateur est généré localement ; il n'a pas été installé ni publié.

Preuves locales ignorées par Git : `artifacts/scope-tests.log`,
`artifacts/ux-preview/probe-profile/scope-probe.json`,
`artifacts/ux-1.0.5/` (captures, binaire, installateur et SHA256SUMS).
Le profil et les outils de sonde sont activés uniquement avec `-p:UxTest=true` ;
le paquet final est construit avec `UxTest=false` et `UpdatePreview=false`.
