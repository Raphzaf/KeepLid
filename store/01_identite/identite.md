# Identité du produit

| Élément | Valeur | Où | Statut |
|---|---|---|---|
| Nom réservé | **KeepLid** (alternatives si pris : « KeepLid – Keep Awake », « KeepLid Awake ») | Partner Center › Applications et jeux › Nouveau produit › *Application MSIX ou PWA* › Réserver le nom | ☐ à faire |
| Package/Identity/Name | `<valeur de Partner Center>` (ex. `12345Editeur.KeepLid`) | Partner Center › Gestion des produits › **Identité du produit** | ☐ à recopier |
| Package/Identity/Publisher | `CN=<GUID de Partner Center>` (ex. `CN=1A2B3C4D-…`) | idem | ☐ à recopier |
| Package/Properties/PublisherDisplayName | **`RaphZaf`** (« nom complet d'éditeur » du compte) | idem | ✅ dans le manifeste |
| Package Family Name (PFN) | `<…>_<hash>` | idem (information) | — |
| Store ID | `9XXXXXXXXXXX` | idem (lien `ms-windows-store://pdp/?ProductId=…`) | — |
| Version | **1.0.0.0** (le 4ᵉ nombre doit rester 0 pour le Store) | `build/package.ps1 -Version` / manifeste | ✅ |
| Architectures | **x64** et **ARM64** (un `.msixbundle`) | `build/package.ps1 -Architectures x64,arm64` | ✅ |
| Systèmes | Windows 10 2004 (10.0.19041) → Windows 11 (testé jusqu'à 10.0.26100) | `TargetDeviceFamily` du manifeste | ✅ |
| Langues du paquet | en-US, fr-FR | `<Resources>` du manifeste + `resources.pri` | ✅ |
| Capacités | `runFullTrust` (restreinte) | manifeste | ✅ justification prête |
| Extensions | `windows.startupTask` (désactivée par défaut), `windows.appExecutionAlias` (`keeplid.exe`) | manifeste | ✅ |

## Où reporter les valeurs d'identité

**Une seule source : `src/KeepLid.Package/Package.appxmanifest`** (`<Identity Name=… Publisher=…>` et
`<PublisherDisplayName>`). Le script `build/package.ps1`, le projet Visual Studio et la CI la lisent tous.
La CI signe ses paquets de test avec un certificat jetable dont le sujet est ce même `Publisher` : le même build
donne le `.msixupload` pour le Store **et** des paquets installables en local.

Le Store refuse le paquet si l'une des trois valeurs diffère de Partner Center (erreur constatée avec les valeurs de
test : « L'élément PublisherDisplayName … est KeepLid, qui ne correspond pas à votre nom complet d'éditeur : RaphZaf »).

Avec Visual Studio, *KeepLid.Package › Publier › Associer l'application au Store…* écrit ces valeurs dans le même
fichier.
