# Identité du produit

| Élément | Valeur | Où | Statut |
|---|---|---|---|
| Nom réservé | **KeepLid** (alternatives si pris : « KeepLid – Keep Awake », « KeepLid Awake ») | Partner Center › Applications et jeux › Nouveau produit › *Application MSIX ou PWA* › Réserver le nom | ☐ à faire |
| Package/Identity/Name | `<valeur de Partner Center>` (ex. `12345Editeur.KeepLid`) | Partner Center › Gestion des produits › **Identité du produit** | ☐ à recopier |
| Package/Identity/Publisher | `CN=<GUID de Partner Center>` (ex. `CN=1A2B3C4D-…`) | idem | ☐ à recopier |
| Package/Properties/PublisherDisplayName | `<nom d'éditeur affiché>` | idem | ☐ à recopier |
| Package Family Name (PFN) | `<…>_<hash>` | idem (information) | — |
| Store ID | `9XXXXXXXXXXX` | idem (lien `ms-windows-store://pdp/?ProductId=…`) | — |
| Version | **1.0.0.0** (le 4ᵉ nombre doit rester 0 pour le Store) | `build/package.ps1 -Version` / manifeste | ✅ |
| Architectures | **x64** et **ARM64** (un `.msixbundle`) | `build/package.ps1 -Architectures x64,arm64` | ✅ |
| Systèmes | Windows 10 2004 (10.0.19041) → Windows 11 (testé jusqu'à 10.0.26100) | `TargetDeviceFamily` du manifeste | ✅ |
| Langues du paquet | en-US, fr-FR | `<Resources>` du manifeste + `resources.pri` | ✅ |
| Capacités | `runFullTrust` (restreinte) | manifeste | ✅ justification prête |
| Extensions | `windows.startupTask` (désactivée par défaut), `windows.appExecutionAlias` (`keeplid.exe`) | manifeste | ✅ |

## Où reporter les valeurs d'identité

Deux méthodes, au choix :

1. **Ligne de commande (recommandé, reproductible)** — les passer à `build/package.ps1` :
   ```powershell
   .\build\package.ps1 -Version 1.0.0.0 `
     -IdentityName "<Package/Identity/Name>" `
     -Publisher "<Package/Identity/Publisher>" `
     -PublisherDisplayName "<Package/Properties/PublisherDisplayName>"
   ```
2. **Visual Studio** — ouvrir `KeepLid.Store.sln`, clic droit sur *KeepLid.Package* › *Publier* ›
   *Associer l'application au Store…* : Visual Studio réécrit `Package.appxmanifest` avec ces valeurs.

Les valeurs `KeepLid` / `CN=KeepLid Test` présentes dans `src/KeepLid.Package/Package.appxmanifest` servent
uniquement aux tests locaux (certificat auto-signé).
