# PLAN — KeepLid

> « Ferme le capot, ton IA continue. »
> Application de zone de notification Windows 10/11 (WPF, .NET 8, MSIX) qui empêche la mise en veille
> pendant qu'une tâche longue tourne, y compris capot fermé, puis restaure tout.

Nom de l'application (`<NOM_APP>`) : **KeepLid**.
Dossier de données : `%LOCALAPPDATA%\KeepLid\` (redirigé par MSIX vers
`%LOCALAPPDATA%\Packages\<PFN>\LocalCache\Local\KeepLid\` ; transparent pour l'app).

## Contraintes environnementales de ce dépôt

Le développement se fait dans un conteneur **Linux** :

* compilation de toutes les cibles possible avec `EnableWindowsTargeting=true` ;
* tests unitaires (`KeepLid.Core`, multiplateforme) exécutés localement ;
* tout ce qui exige Windows (spike, paquet MSIX, installation, WACK) est exécuté par
  **GitHub Actions** sur `windows-latest` (`.github/workflows/`), et les journaux servent de preuve.

## Étapes et critères de fin

| # | Étape | Critère de fin |
|---|-------|----------------|
| 0 | **Spike** `spike/` : console .NET 8 qui lit / écrit 0 / relit / restaure l'action du capot (AC + DC) | Exécuté sur Windows **en utilisateur standard** (exe nu **et** depuis un paquet MSIX) ; résultat et codes d'erreur dans `docs/SPIKE_RESULT.md`. **STOP si ACCESS_DENIED.** |
| 1a | `KeepLid.Core` : machine à états, minuterie, surveillance de processus, garde-fous, `restore.json`, réglages, journal, textes FR/EN (.resx) | Compile, aucune dépendance Windows, couvert par les tests |
| 1b | `KeepLid.App` (WPF + icône de notification) : interop Win32, menu, réglages, accueil, raccourci global, toasts, StartupTask, chien de garde | Compile sans avertissement bloquant ; démarre sur Windows (CI : lancement + arrêt propre) |
| 1c | Packaging MSIX : `Package.appxmanifest`, projet `.wapproj` (Visual Studio) + script `build/package.ps1` (CI) ; x64 + ARM64 ; icônes générées depuis un SVG maître | `.msix` x64 et ARM64 + `.msixbundle` + `.msixupload` produits par la CI |
| 2 | Qualité : tests xUnit (minuterie, processus, états, `restore.json` + crash simulé), `docs/TESTS_MANUELS.md`, WACK | Tous les tests verts ; WACK lancé en CI, erreurs corrigées ou documentées |
| 3 | Dossier `store/` complet (arborescence imposée) | Tous les fichiers remplis, prêts à copier-coller |
| 4 | `README.md`, `docs/BUILD_ET_PACKAGING.md`, `docs/ARCHITECTURE.md`, `CHANGELOG.md`, `artifacts/` | Livrables présents ; résumé final |

## Avancement

| # | Statut |
|---|---|
| 0 | ⛔ STOP déclenché (`ERROR_ACCESS_DENIED` sans admin sur Windows Server 2022/2025, exe nu et MSIX) — voir `docs/SPIKE_RESULT.md`. **Décision : option (a+)** — détection au démarrage, mode capot automatique si permis, guide pas à pas sinon. |
| 1a | ✅ Core + 72 tests |
| 1b | ✅ App WPF (compile sans avertissement, smoke test CI : démarrage, restauration après crash, chien de garde, 0 % CPU au repos) |
| 1c | ✅ MSIX x64 + ARM64, bundle, `.msixupload` (script + projet Visual Studio), icônes générées |
| 2 | ✅ tests xUnit, `docs/TESTS_MANUELS.md`, WACK en CI |
| 3 | ✅ `store/` complet (marqueur `<EMAIL_SUPPORT>` à remplacer ; éditeur : RaphZaf) |
| 4 | ✅ docs, README, CHANGELOG ; paquets produits par la CI (artefact `keeplid-packages`) |

## Décisions techniques

* **Interop** : `DllImport` explicites (lisibles), chaque signature et GUID référencé au fichier d'en-tête
  du Windows SDK 10.0.26100 (`powersetting.h`, `powrprof.h`, `winnt.h`, `WinBase.h`) — paquet NuGet officiel
  `Microsoft.Windows.SDK.CPP`, vérifié dans ce dépôt.
* **Capot sur batterie** : par défaut seule la valeur **secteur (AC)** est modifiée ; la valeur batterie (DC)
  n'est modifiée qu'après confirmation explicite (une seule fois, mémorisée).
* **Restauration** : `restore.json` écrit **avant** toute écriture système ; restauration à la désactivation,
  à la sortie, `SessionEnding`, mise en veille / reprise, exception non gérée, et au lancement suivant.
  En plus : un **processus chien de garde** (même exe, `--watchdog <pid> <restore.json> <log>`) restaure si l'app est tuée
  (Gestionnaire des tâches) pendant que le mode capot est actif.
* **Icône de notification** : `System.Windows.Forms.NotifyIcon` (aucune dépendance tierce).
* **Notifications** : info-bulles de l'icône (affichées comme toasts par Windows 10/11).
* **Démarrage avec Windows** : `StartupTask` MSIX (désactivé par défaut).
* **Licence / essai** : essai Store géré via `Windows.Services.Store` (lecture locale de la licence).
