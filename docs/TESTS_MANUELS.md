# Tests manuels

Compléments des tests automatiques (72 tests xUnit + test d'installation en CI : restauration après crash,
CPU / mémoire au repos, WACK). À dérouler avant chaque soumission, sur le paquet MSIX installé
(voir `docs/BUILD_ET_PACKAGING.md` §3 pour l'installer avec le certificat de test).

## 0. Outils de vérification

Action actuelle du capot (mode actif) — **à lancer avant et après chaque test** :

```powershell
powercfg /q SCHEME_CURRENT SUB_BUTTONS LIDACTION
```

Lire les deux lignes `Current AC Power Setting Index` (secteur) et `Current DC Power Setting Index` (batterie) :
`0x00000000` = Ne rien faire, `0x00000001` = Veille, `0x00000002` = Veille prolongée, `0x00000003` = Arrêter.
Sur un PC sans capot, le réglage est masqué ; l'afficher (admin) avec
`powercfg -attributes SUB_BUTTONS LIDACTION -ATTRIB_HIDE`.

Autres commandes utiles :

```powershell
powercfg /getactivescheme            # mode d'alimentation actif
powercfg /requests                   # KeepLid doit apparaître sous SYSTEM (et DISPLAY si « écran allumé ») quand actif
```

Dossier de données de l'app installée : `%LOCALAPPDATA%\Packages\RaphZaf.KeepLid_2yg2xyjs83sm8\LocalCache\Local\KeepLid\`
(`settings.json`, `restore.json` pendant le mode capot uniquement, `log.txt`). Réglages › « Ouvrir le journal » y mène.

Noter pour chaque test : édition et build de Windows (`winver`), architecture, compte (admin avec UAC / standard),
résultat de la détection (`log.txt` : « Lid access probe: write allowed » ou « refused »).

## 1. Capot fermé sur secteur (mode automatique)
1. Secteur branché. Noter la valeur AC (ex. `1`).
2. Clic droit › cocher « Continuer quand le capot est fermé », puis Garder éveillé › 30 minutes.
3. Attendu : icône bleue + badge vert ; info-bulle « Éveillé — capot OK — encore 30 min » ; `restore.json` présent ; `powercfg` AC = `0`, DC inchangé.
4. Lancer une tâche visible (ex. `ping -t 1.1.1.1 > %TEMP%\ping.txt`), fermer le capot 5 min, rouvrir.
5. Attendu : écran interne éteint pendant la fermeture (normal), le fichier ping a continué de grossir, pas de mise en veille.
6. Clic gauche (désactiver). Attendu : icône grise ; AC revenu à la valeur notée ; `restore.json` supprimé.

## 2. Capot fermé sur batterie
1. Secteur débranché, batterie > 30 %. Activer le mode capot.
2. Attendu : **une seule fois**, la fenêtre « Mode capot sur batterie » ; répondre **Non** → seul AC passe à `0`, DC inchangé (capot fermé sur batterie ⇒ veille normale).
3. Réglages › cocher « Mode capot aussi sur batterie » → confirmer **Oui** → DC passe à `0`.
4. Réglages : durée max capot fermé = 5 min. Fermer le capot 6 min. Attendu : arrêt par garde-fou (icône orange, notification), AC/DC restaurés.
5. Seuil batterie : régler le seuil au-dessus du niveau actuel → arrêt immédiat avec notification « Batterie sous N % ».

## 3. Kill via le Gestionnaire des tâches puis relance
1. Activer le mode capot (AC = `0`).
2. Gestionnaire des tâches › Détails › `KeepLid.exe` (le plus gros, pas `--watchdog`) › Fin de tâche.
3. Attendu (≤ 3 s) : le chien de garde restaure AC ; `log.txt` : « Watchdog: KeepLid … ended without restoring ».
4. Recommencer en tuant **les deux** processus `KeepLid.exe` (ou « Fin de tâche » sur le groupe dans l'onglet Processus).
5. Attendu : AC reste `0` tant que KeepLid n'est pas relancé ; relancer KeepLid → restauration immédiate + notification « Réglages d'alimentation restaurés ».

## 4. Redémarrage / arrêt pendant l'activité
1. Activer le mode capot, puis Démarrer › Redémarrer.
2. Attendu après redémarrage : AC à la valeur d'origine (`SessionEnding`). `log.txt` : « Session ending … restoring ».
3. Variante coupure brutale (maintenir le bouton d'alimentation) : AC peut rester `0` jusqu'au lancement suivant de KeepLid (immédiat si « Lancer au démarrage » est activé).

## 5. Changement de mode d'alimentation pendant l'activité
1. Activer le mode capot sur le mode « Utilisation normale » (Balanced).
2. Panneau de configuration › Options d'alimentation › choisir un autre mode (ex. « Performances élevées » ; sous Windows 11, `powercfg /setactive SCHEME_MIN` si le mode n'est pas proposé).
3. Attendu (≤ 5 s) : l'ancien mode a retrouvé sa valeur, le nouveau est à `0` ; `restore.json` contient le GUID du nouveau mode.
4. Désactiver : le nouveau mode retrouve sa valeur.
5. Changement manuel du réglage pendant l'activité (mettre « Veille prolongée » dans les Options d'alimentation) puis désactiver : la valeur choisie par l'utilisateur est **conservée** (log : « changed by the user »).

## 6. Mode guide (Windows refuse l'écriture)
À tester avec un **compte standard** (non administrateur) et avec un compte administrateur (UAC actif).
1. Lancer KeepLid. Si `log.txt` indique « write refused » : le menu propose « Capot fermé : guide de réglage… ».
2. Ouvrir le guide › « Ouvrir les réglages du capot » : la page *Options d'alimentation › Paramètres système* s'ouvre ; la ligne « Réglage actuel » se met à jour en ≤ 2 s après « Enregistrer les modifications ».
3. Démarrer puis arrêter une session : notification de rappel si l'action est encore « Ne rien faire ».
4. Vérifier qu'aucune invite UAC n'apparaît jamais.

## 7. PC sans capot (fixe ou VM)
1. Lancer KeepLid. Attendu : « Continuer quand le capot est fermé (aucun capot détecté) » grisé ; Réglages : « Aucun capot détecté sur ce PC ».
2. Garder éveillé 30 min : `powercfg /requests` montre KeepLid ; pas de veille après le délai d'inactivité configuré.

## 8. Fonctions générales
| # | Test | Attendu |
|---|---|---|
| 8.1 | Premier lancement | Accueil 3 étapes ; ne réapparaît plus ensuite (Réglages › « Revoir l'écran d'accueil ») |
| 8.2 | Clic gauche / double-clic rapide | bascule ; un double-clic ne bascule qu'une fois |
| 8.3 | Garder éveillé jusqu'à la fin d'un processus (`notepad`) | arrêt ≈ 15 s après la fermeture de Notepad + toast « Tâche terminée » |
| 8.4 | Durée personnalisée 2 min | arrêt après 2 min + toast |
| 8.5 | « Garder l'écran allumé » | `powercfg /requests` › DISPLAY ; l'écran ne s'éteint pas après le délai |
| 8.6 | Raccourci `Ctrl+Alt+Maj+A` | bascule ; changer le raccourci dans Réglages ; conflit → notification |
| 8.7 | Lancer au démarrage | coché → présent dans Paramètres › Applications › Démarrage ; redémarrer → icône présente |
| 8.8 | Mise en veille manuelle (Démarrer › Veille) pendant l'activité | à la reprise : session terminée, réglages restaurés, notification |
| 8.9 | Langue | Windows en anglais → interface anglaise ; en français → française |
| 8.10 | Journal | > 1 Mo → `log.1.txt` créé ; « Ouvrir le journal » sélectionne le fichier dans l'Explorateur |
| 8.11 | Deuxième lancement | aucune 2ᵉ icône (instance unique) |
| 8.12 | Quitter | AC/DC d'origine, `powercfg /requests` vide pour KeepLid |

## 9. Accessibilité (avant de cocher la déclaration Store)
- Narrateur (Ctrl+Win+Entrée) : chaque contrôle des fenêtres est annoncé ; navigation complète au clavier (Tab / Entrée / Échap).
- Contraste élevé (Alt+Maj gauche+Impr. écran) : fenêtres lisibles (couleurs système).
- Mise à l'échelle 150 % et 200 % : pas de texte coupé ; icône nette.

## 10. Performance (cible : ~0 % CPU au repos, < 50 Mo de RAM)
Gestionnaire des tâches › Détails, colonnes *Processeur*, *Plage de travail (mémoire)*, *Mémoire (plage de travail privée)* :
- inactif depuis 1 min : CPU 0 %, plage de travail privée < 50 Mo ;
- actif (tick toutes les 5 s) : CPU ≈ 0 % ;
- chien de garde (pendant le mode capot uniquement) : quelques Mo, 0 % CPU.
La CI mesure aussi ces valeurs (étape *Smoke test*).

## 11. Matrice
| Configuration | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|
| Windows 11 x64, portable, admin + UAC | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | — | ☐ |
| Windows 11 x64, portable, compte standard | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | — | ☐ |
| Windows 11 ARM64, portable (Snapdragon) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | — | ☐ |
| Windows 10 22H2 x64, portable | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | — | ☐ |
| PC fixe Windows 11 (sans capot) | — | — | — | ☐ | — | — | ☐ | ☐ |
| Portable en veille moderne (S0 Low Power Idle, `powercfg /a`) | ☐ | ☐ | ☐ | ☐ | ☐ | ☐ | — | ☐ |

Point d'attention « veille moderne » : sur ces PC, vérifier spécialement le test 1 (capot fermé sur secteur, 10 min)
et noter dans `log.txt` / `powercfg /sleepstudy` si le système est entré en veille malgré « Ne rien faire ».
