# Phase 0 — Résultat du spike de faisabilité

**Statut : ⛔ STOP — l'écriture de l'action du capot échoue sans droits administrateur (`ERROR_ACCESS_DENIED`, code 5)
sur toutes les machines de test disponibles.** Décision requise avant de continuer (voir § 5).

Date : 2026-11-08 · Code : [`spike/KeepLid.Spike/Program.cs`](../spike/KeepLid.Spike/Program.cs) ·
Script : [`spike/run-spike.ps1`](../spike/run-spike.ps1) · CI : [`.github/workflows/spike.yml`](../.github/workflows/spike.yml)

## 1. Ce qui a été testé

Le spike (console .NET 8, `DllImport` sur `powrprof.dll`) :

1. lit l'action de fermeture du capot (AC + DC) du mode d'alimentation actif ;
2. écrit `0` (Ne rien faire) en AC et DC, réapplique le mode actif (`PowerSetActiveScheme`), relit ;
3. restaure les valeurs d'origine (toujours, dans un `finally`), réapplique, relit ;
4. (sonde de l'alternative b) duplique le mode actif (`PowerDuplicateScheme`), modifie la copie, l'active,
   revient au mode d'origine, supprime la copie.

Constantes vérifiées dans les en-têtes officiels du Windows SDK 10.0.26100 (paquet NuGet `Microsoft.Windows.SDK.CPP`) :

| Élément | Valeur | Source |
|---|---|---|
| `GUID_SYSTEM_BUTTON_SUBGROUP` | `4f971e89-eebd-4455-a8de-9e59040e7347` | `um/winnt.h` |
| `GUID_LIDCLOSE_ACTION` | `5ca83367-6e45-459f-a27b-476b1d01c936` | `um/winnt.h` |
| Valeurs | `POWERBUTTON_ACTION_INDEX_NOTHING 0`, `_SLEEP 1`, `_HIBERNATE 2`, `_SHUTDOWN 3` | `um/winnt.h` |
| `PowerWriteACValueIndex`, `PowerWriteDCValueIndex`, `PowerGetActiveScheme`, `PowerSetActiveScheme` | signatures | `um/powersetting.h` |
| `PowerReadACValueIndex`, `PowerReadDCValueIndex`, `PowerDuplicateScheme`, `PowerDeleteScheme` | signatures | `um/powrprof.h` |
| `ERROR_ACCESS_DENIED` | `5` | `shared/winerror.h` |

## 2. Environnement

Le conteneur de développement est sous Linux : le spike a été exécuté par GitHub Actions
(run [42496584302](https://github.com/Raphzaf/KeepLid/actions/runs/42496584302)) sur :

| Image | OS | Mode d'alimentation actif | Capot détecté |
|---|---|---|---|
| `windows-2025` | Windows Server 2025 Datacenter 10.0.26100 | High performance `8c5e7fda-…` | non (VM) |
| `windows-2022` | Windows Server 2022 Datacenter 10.0.20348 | High performance `8c5e7fda-…` | non (VM) |

Quatre configurations par machine :

* **admin** : compte `runneradmin` (jeton administrateur complet, UAC désactivé sur les runners) ;
* **standard** : compte local `klstd` créé pour le test, membre de *Users* uniquement, lancé via
  `Start-Process -Credential` (ouverture de session interactive) ;
* **exe nu** et **MSIX** (paquet signé avec un certificat de test, `runFullTrust`, lancé par alias d'exécution ;
  identité de paquet confirmée : `KeepLid.Spike_1.0.0.0_x64__bdejnz2j837kj`).

## 3. Résultats (identiques sur Server 2022 et Server 2025)

| Étape | admin / exe | admin / MSIX | **standard / exe** | **standard / MSIX** |
|---|---|---|---|---|
| `PowerGetActiveScheme` | 0 | 0 | 0 | 0 |
| `PowerRead{AC,DC}ValueIndex` (origine = 1/1) | 0 | 0 | 0 | 0 |
| `PowerWriteACValueIndex` (→ 0) | 0 | 0 | **5 ACCESS_DENIED** | **5 ACCESS_DENIED** |
| `PowerWriteDCValueIndex` (→ 0) | 0 | 0 | **5 ACCESS_DENIED** | **5 ACCESS_DENIED** |
| `PowerSetActiveScheme` (même mode) | 0 | 0 | **5 ACCESS_DENIED** | **5 ACCESS_DENIED** |
| Relecture après écriture | 0/0 ✅ | 0/0 ✅ | 1/1 (inchangé) | 1/1 (inchangé) |
| Restauration + relecture | 1/1 ✅ | 1/1 ✅ | 1/1 (rien à restaurer) | 1/1 (rien à restaurer) |
| `PowerDuplicateScheme` (alternative) | 0 | 0 | **5 ACCESS_DENIED** | **5 ACCESS_DENIED** |
| Code de sortie du spike | 0 | 0 | **2** | **2** |

Conclusions :

* **MSIX ne change rien** : le comportement est identique en exe nu et en paquet (pas de virtualisation : les API
  passent par le service d'alimentation, qui contrôle l'accès avec l'identité de l'appelant).
* La lecture est toujours permise ; **l'écriture, l'activation d'un mode et la création d'un mode exigent les droits
  administrateur** sur ces machines.

## 4. Pourquoi : le descripteur de sécurité du mode d'alimentation

`powercfg /getsecuritydescriptor SCHEME_CURRENT` (identique sur les deux images) :

```
O:BAG:SYD:P(A;CI;KR;;;BU)(A;CI;KA;;;BA)(A;CI;KA;;;SY)(A;CI;KA;;;CO)(A;CI;KR;;;S-1-15-2-1)(A;CI;KR;;;S-1-15-3-1024-…)
```

| SID | Droit |
|---|---|
| `BU` Utilisateurs | **KR = lecture seule** |
| `BA` Administrateurs | KA = contrôle total |
| `SY` SYSTEM | KA |
| `CO` Créateur propriétaire | KA |
| AppContainers | KR |

Conséquence importante : sur un PC où l'utilisateur est **administrateur avec UAC** (cas le plus courant chez les
développeurs), l'app tourne avec un jeton **filtré** où le SID *Administrateurs* est « deny-only ». Avec ce
descripteur, elle obtiendrait elle aussi `ACCESS_DENIED`, sauf à être lancée élevée.

### Limite de ce test

Les seules machines Windows disponibles en CI sont des **Windows Server** (pas de Windows 10/11 client, pas de capot).
Les ACL par défaut des modes d'alimentation **peuvent être différentes sur une édition client** — c'est précisément ce
qu'il faut vérifier. Test décisif, 1 minute, sur un vrai portable Windows 10/11, **dans un terminal normal (non administrateur)** :

```powershell
powercfg /getsecuritydescriptor SCHEME_CURRENT
.\KeepLid.Spike.exe --out spike-result.json
```

L'exécutable (`keeplid-spike-win-x64` ou `keeplid-spike-win-arm64`) est publié comme artefact par le workflow
`spike` (job `spike-exe`). Le spike est sans danger : il remet toujours la valeur d'origine.
Code de sortie : `0` = faisable sans admin, `2` = écriture refusée.

## 5. Options (décision requise)

**(a) MVP sans mode capot automatique.** Anti-veille + écran allumé (`SetThreadExecutionState`, aucun droit
requis), minuterie, surveillance de processus, garde-fous batterie. Le mode capot devient un **guide pas à pas**
(Paramètres › Système › Alimentation › « Fermer le capot ») ; l'app **lit** le réglage courant (lecture autorisée),
l'affiche, et rappelle de le remettre à la fin.
Risque certification : aucun. Risque produit : la promesse « Ferme le capot » devient manuelle.

**(a+) Recommandé — (a) avec détection automatique.** Au démarrage, l'app sonde l'accès en réécrivant la valeur
**déjà en place** (aucun changement effectif). Si l'écriture est permise (édition client permissive, ACL
d'entreprise, etc.), le mode capot automatique complet est activé, avec tout le mécanisme de sauvegarde /
restauration prévu ; sinon, l'app bascule sur le guide de (a). Même code, aucune élévation, aucun risque de
certification ; le comportement s'adapte à la machine. La fiche Store devra rester prudente (« lorsque Windows le
permet »).

**(b) Alternatives techniques étudiées**, toutes avec élévation, donc en conflit avec la contrainte n° 1 :

| Approche | Fonctionne ? | Risques |
|---|---|---|
| Copie du mode (`PowerDuplicateScheme`) appartenant à l'utilisateur (`CO` = KA) | ❌ création refusée (5) | — |
| Invite UAC à chaque activation / désactivation (`powercfg` élevé) | ✅ techniquement | Viole « aucun droit admin » ; restauration après crash impossible sans nouvelle invite ; UX médiocre ; Store : app qui s'élève = vérification renforcée. |
| Élévation unique pour élargir l'ACL du mode (`PowerWriteSecurityDescriptor`) | ✅ techniquement | Modification **permanente** de la sécurité système, non restaurée à la désinstallation (MSIX n'a pas de hook de désinstallation) : viole les contraintes 1 et 3, rejet Store probable. |
| Capacité restreinte `allowElevation` (MSIX) | ✅ techniquement | Capacité restreinte supplémentaire à justifier ; même problème d'invite UAC ; viole la contrainte 1. |

Recommandation : **(a+)**, après avoir lancé le test décisif ci-dessus sur un portable Windows 11 réel. Si le résultat
client est `0` (succès sans admin), (a+) se comporte comme le produit complet prévu, sans changer le code.
