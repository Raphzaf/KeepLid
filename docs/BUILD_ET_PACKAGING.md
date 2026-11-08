# Compilation et packaging

Deux chemins équivalents, à partir des **mêmes** fichiers (`src/KeepLid.Package/Package.appxmanifest` + `Images/`) :

* **A — ligne de commande** (`build/package.ps1`) : reproductible, utilisé par la CI, pas besoin de Visual Studio ;
* **B — Visual Studio 2022** (`KeepLid.Store.sln` + projet `KeepLid.Package.wapproj`) : assistant « Associer au Store ».

## 1. Prérequis (Windows 10/11 x64)

| Outil | Version | Installation |
|---|---|---|
| .NET SDK | 8.0.x | `winget install Microsoft.DotNet.SDK.8` |
| Windows SDK (makeappx, makepri, signtool, App Certification Kit) | 10.0.19041 ou plus récent (testé 10.0.26100) | `winget install Microsoft.WindowsSDK.10.0.26100` |
| PowerShell | 5.1 (intégré) ou 7 | — |
| *(chemin B)* Visual Studio 2022 | 17.8+ | charge de travail « Développement .NET Desktop » + composant « Outils d'empaquetage MSIX » |

Compiler et tester seulement :

```powershell
dotnet build KeepLid.sln -c Release
dotnet test tests/KeepLid.Core.Tests -c Release
dotnet run --project src/KeepLid.App        # lance l'app non empaquetée (sans StartupTask ni licence Store)
```

> Sous Linux/macOS, `KeepLid.Core` et ses tests se compilent et passent ; l'app WPF se compile aussi
> (`EnableWindowsTargeting`) avec le SDK .NET **de Microsoft** (pas la version « source-build » des distributions).

## 2. Produire le `.msixupload` pour le Store (Release, x64 + ARM64)

### Chemin A — ligne de commande

Les trois valeurs de Partner Center › *Gestion des produits* › **Identité du produit** (`Package/Identity/Name`,
`Package/Identity/Publisher`, `Package/Properties/PublisherDisplayName`) doivent figurer dans
`src/KeepLid.Package/Package.appxmanifest` (voir `store/01_identite/identite.md`). Ensuite :

```powershell
.\build\package.ps1 -Version 1.0.0.0
```

(`-IdentityName`, `-Publisher`, `-PublisherDisplayName` permettent de les surcharger ponctuellement.)
La CI produit le même `.msixupload` : artefact **keeplid-packages** du workflow **ci**.

Le script : lance les tests ; publie l'app **autonome** (`--self-contained`, aucun runtime .NET requis chez
l'utilisateur) pour `win-x64` et `win-arm64` ; génère `resources.pri` (`makepri`, langues en-US + fr-FR) ;
crée un `.msix` par architecture (`makeappx pack`) ; les regroupe dans un `.msixbundle` (`makeappx bundle`) ;
emballe le bundle dans le **`.msixupload`**.

Résultat dans `artifacts/` :

```
KeepLid_1.0.0.0_x64.msix
KeepLid_1.0.0.0_arm64.msix
KeepLid_1.0.0.0_x64_arm64.msixbundle
KeepLid_1.0.0.0_x64_arm64.msixupload   ← à déposer dans Partner Center › Packages
```

Le paquet Store **n'a pas à être signé** : le Store le signe lui-même. Ne pas téléverser un paquet signé avec un
certificat de test.

### Chemin B — Visual Studio
1. Ouvrir `KeepLid.Store.sln`, configuration **Release**, plateforme **x64**.
2. Clic droit sur **KeepLid.Package** › *Publier* › **Associer l'application au Store…** › se connecter › choisir
   *KeepLid* (réécrit l'identité dans `Package.appxmanifest`).
3. Clic droit › *Publier* › **Créer des packages d'application…** › *Microsoft Store sous un nom d'application
   existant* › cocher **x64** et **ARM64** (Release), « Générer le bundle : Toujours » › *Créer*.
4. Le `.msixupload` est dans `artifacts/AppPackages/`. L'assistant propose de lancer le WACK (cocher).

Équivalent en ligne de commande (Developer PowerShell for VS) :

```powershell
msbuild src\KeepLid.Package\KeepLid.Package.wapproj /restore /p:Configuration=Release /p:Platform=x64 `
  /p:AppxBundle=Always "/p:AppxBundlePlatforms=x64|arm64" /p:UapAppxPackageBuildMode=StoreUpload
```

## 3. Tester le paquet en local (certificat de test)

Le `Publisher` du manifeste doit être **identique** au sujet du certificat : utiliser la valeur `Publisher` du
manifeste (ex. `CN=1A2B3C4D-…`) comme `-Subject` ci-dessous.

1. Créer le certificat (une fois, PowerShell **non** élevé) :
   ```powershell
   $subject = ([xml](Get-Content src\KeepLid.Package\Package.appxmanifest -Raw)).Package.Identity.Publisher
   $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
     -CertStoreLocation Cert:\CurrentUser\My -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
   $pwd = Read-Host -AsSecureString "Mot de passe du .pfx"
   Export-PfxCertificate -Cert $cert -FilePath .\KeepLidTest.pfx -Password $pwd
   Export-Certificate -Cert $cert -FilePath .\KeepLidTest.cer
   ```
2. Construire et signer :
   ```powershell
   .\build\package.ps1 -CertificatePath .\KeepLidTest.pfx -CertificatePassword $pwd
   ```
3. Faire confiance au certificat (PowerShell **administrateur**, une fois) :
   ```powershell
   Import-Certificate -FilePath .\KeepLidTest.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
4. Installer : double-clic sur `artifacts\KeepLid_1.0.0.0_x64.msix` (ou le `.msixbundle`), ou
   `Add-AppxPackage .\artifacts\KeepLid_1.0.0.0_x64.msix`. Lancer depuis le menu Démarrer ou `keeplid` (Win+R).
5. Désinstaller : `Get-AppxPackage *KeepLid* | Remove-AppxPackage`.

Les `.pfx` sont exclus du dépôt (`.gitignore`). Le paquet de la CI (artefact `keeplid-packages`) est signé avec un
certificat jetable ; `KeepLidTest.cer` est fourni dans l'artefact pour l'installer.

## 4. Windows App Certification Kit (WACK)

PowerShell **administrateur**, sur le `.msix` x64 signé et approuvé (§3) :

```powershell
.\build\run-wack.ps1 -MsixPath .\artifacts\KeepLid_1.0.0.0_x64.msix
```

Ou interface graphique : menu Démarrer › *Windows App Cert Kit* › *Valider l'application du Store*.
Le rapport XML (`wack-report.xml`) liste chaque test ; la CI l'exécute et publie le rapport (artefact `wack-report`).

**Résultat CI (8 nov. 2026, run [42500017375](https://github.com/Raphzaf/KeepLid/actions/runs/42500017375), WACK du
Windows SDK 10.0.26100, paquet x64) : `OVERALL_RESULT = PASS`.** Seul le test *Package sanity › Blocked executables*
liste des éléments ; ce test n'empêche pas le résultat global « Réussi » et signale :

* des chaînes « cmd », « reg », « bash », « MSBuild »… présentes dans les DLL du runtime .NET / WPF / WinForms
  (faux positifs connus de toute application .NET autonome) ;
* des références aux API de lancement de processus, dont celles de KeepLid, toutes légitimes : démarrage du chien de
  garde (`KeepLid.exe --watchdog`), ouverture de l'Explorateur sur le journal, du Panneau de configuration (guide du
  capot) et des liens d'aide / confidentialité dans le navigateur.

Aucune action requise ; à mentionner si un testeur pose la question.

## 5. Test d'installation automatisé (CI)

`build/smoke-test.ps1` (administrateur, machine jetable) : installe le `.msix`, simule un crash (action du capot à
« Ne rien faire » + `restore.json` laissé dans le dossier de l'app), lance KeepLid, vérifie la **restauration** ;
teste le **chien de garde** (processus surveillé tué → restauration) ; mesure le CPU et la mémoire au repos ;
affiche le journal.

Résultat CI (Windows Server 2022, run 42500017375) :

| Vérification | Résultat |
|---|---|
| Restauration au lancement après crash simulé | ✅ action du capot remise de 0 à 1, `restore.json` supprimé |
| Chien de garde (processus tué) | ✅ restauration en ≈ 1 s |
| CPU au repos (30 s) | ✅ 0,000 % |
| Mémoire au repos | ✅ plage de travail privée 1,5 Mo (colonne « Mémoire » du Gestionnaire des tâches), plage de travail 5,1 Mo, octets privés 20 Mo |
| Licence sur paquet non-Store | ✅ vérification ignorée (« package signature: Developer ») |

Tailles : `.msix` x64 69 Mo, ARM64 65 Mo, `.msixupload` 133 Mo (runtime .NET 8 inclus).

## 6. Régénérer les icônes

```bash
pip install cairosvg pillow
python store/03_visuels/generate_icons.py
```

Source unique : `store/03_visuels/keeplid.svg`. Sorties : `src/KeepLid.Package/Images/`,
`src/KeepLid.App/Assets/` (icône de l'app et 4 icônes de la zone de notification), `store/03_visuels/icones/`.

## 7. Versionner une mise à jour
1. Choisir la version `X.Y.Z.0` (le 4ᵉ nombre reste `0`), mettre à jour `CHANGELOG.md` et `store/08_release/`.
2. `.\build\package.ps1 -Version X.Y.Z.0 -IdentityName … -Publisher … -PublisherDisplayName …`
3. Nouvelle soumission Partner Center : remplacer le paquet, mettre à jour « Nouveautés ».
