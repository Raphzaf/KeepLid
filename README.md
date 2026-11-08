# KeepLid

> **Ferme le capot, ton IA continue.**

KeepLid est une petite application de la zone de notification Windows 10/11 qui empêche la mise en veille pendant
qu'un agent IA (Claude Code, Codex, Cursor…), une compilation, un téléchargement ou un rendu tourne — y compris
capot fermé lorsque Windows le permet — puis rend la main automatiquement et **restaure tous les réglages**.

* Un clic sur l'icône : activé / désactivé. Minuterie (30 min → 4 h, personnalisée) ou **jusqu'à la fin d'un processus**.
* **Mode capot** : action « Lorsque je ferme le capot » mise temporairement sur « Ne rien faire » (secteur ; batterie
  après confirmation), sauvegardée avant, restaurée après — même après un plantage.
  Si Windows réserve ce réglage aux administrateurs, KeepLid le détecte et propose un **guide pas à pas**
  (jamais d'élévation ; voir [`docs/SPIKE_RESULT.md`](docs/SPIKE_RESULT.md)).
* Garde-fous : batterie faible (20 %), durée max capot fermé sur batterie (2 h). Écran allumé en option.
* 100 % local : aucune connexion, aucune télémétrie, aucun compte, aucun droit administrateur, aucun service.
* Français / anglais, x64 / ARM64, MSIX pour le Microsoft Store.

KeepLid est une application indépendante, sans affiliation avec Anthropic, OpenAI, Anysphere ni d'autres éditeurs.

## Aperçu

Icônes de la zone de notification (inactif, actif, actif + capot, garde-fou) sur fond clair et sombre, et logo Store :

![Icônes](docs/images/icons-preview.png)

Les captures d'écran de l'application sont à réaliser sur Windows selon
[`store/03_visuels/SPEC_CAPTURES.md`](store/03_visuels/SPEC_CAPTURES.md).

## Démarrage rapide (développeurs)

```powershell
dotnet build KeepLid.sln -c Release
dotnet test tests/KeepLid.Core.Tests -c Release
dotnet run --project src/KeepLid.App
```

Paquets MSIX et `.msixupload` pour le Store : [`docs/BUILD_ET_PACKAGING.md`](docs/BUILD_ET_PACKAGING.md)
(`build/package.ps1`, ou Visual Studio avec `KeepLid.Store.sln`).

## Structure

| Dossier | Contenu |
|---|---|
| `src/KeepLid.Core` | logique pure (machine à états, restauration, minuterie, processus, garde-fous, réglages, journal, textes FR/EN) |
| `src/KeepLid.App` | application WPF + icône de notification, interop Win32 |
| `src/KeepLid.Package` | manifeste MSIX, images, projet de packaging Visual Studio |
| `tests/` | tests xUnit (72) |
| `spike/` | spike de faisabilité Phase 0 |
| `build/` | scripts de packaging, test d'installation, WACK |
| `store/` | dossier complet de publication Partner Center |
| `docs/` | architecture, build, tests manuels, résultat du spike |

Documentation : [Architecture](docs/ARCHITECTURE.md) · [Tests manuels](docs/TESTS_MANUELS.md) ·
[Checklist de publication](store/00_CHECKLIST_PUBLICATION.md) · [FAQ](store/07_support/FAQ.md) ·
[Changelog](CHANGELOG.md) · [Plan](PLAN.md)

## Confidentialité

Aucune donnée collectée ni transmise. Voir [`store/04_confidentialite/privacy-fr.html`](store/04_confidentialite/privacy-fr.html).

## Licence

Propriétaire — tous droits réservés (voir [`LICENSE`](LICENSE)). Application distribuée via le Microsoft Store
(achat unique, essai gratuit de 7 jours).
