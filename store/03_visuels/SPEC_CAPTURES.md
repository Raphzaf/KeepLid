# Captures d'écran à réaliser (5)

**Format** : PNG, **1920×1080** recommandé (minimum 1366×768), 16:9, ≤ 50 Mo. Windows 11, thème clair, mise à l'échelle
100 % ou 125 %, fond d'écran sobre, barre des tâches visible pour les captures de la zone de notification.
Ne montrer **aucune** donnée personnelle (nom d'utilisateur, e-mails, chemins privés). Faire une série en **français**
(Windows en fr-FR) et une en **anglais** (Windows en en-US) : l'app suit la langue de Windows.
Outil conseillé : *Outil Capture d'écran* (Win+Maj+S) en mode fenêtre, puis recadrage 16:9.

Pour garder une info-bulle ou un menu ouverts pendant la capture : *Outil Capture* › *Retarder de 5 s*.

| # | Contenu | Mise en scène | Légende FR (≤ 200 car.) | Caption EN (≤ 200 chars) |
|---|---|---|---|---|
| 1 | Menu clic droit de l'icône, sous-menu « Garder éveillé » ouvert | Session active (icône bleue + badge vert), un terminal en arrière-plan avec un agent IA qui travaille (contenu neutre, sans nom de marque à l'écran) | Un clic pour garder le PC éveillé : 30 min, 1 h, 2 h, 4 h, jusqu'à désactivation ou jusqu'à la fin d'un processus. | One click to keep your PC awake: 30 min, 1 h, 2 h, 4 h, until turned off, or until a process ends. |
| 2 | Info-bulle de l'icône « Éveillé — capot OK — encore 1 h 42 » | Zoomer sur la zone de notification (recadrer en 16:9 avec un fond propre) | Ferme le capot, ton IA continue. L'info-bulle indique l'état et le temps restant. | Close the lid, your AI keeps working. The tooltip shows the state and the time left. |
| 3 | Fenêtre « Garder éveillé jusqu'à la fin d'un processus » | `node` et `python` cochés, champ de recherche vide | Reste actif tant que votre agent IA, votre build ou votre téléchargement tourne, puis se désactive tout seul. | Stays on while your AI agent, build or download runs, then turns itself off. |
| 4 | Fenêtre « Réglages » | Garde-fous visibles (20 %, 120 min), bouton « Restaurer les réglages d'alimentation d'origine maintenant » | Garde-fous batterie et durée. Vos réglages d'origine sont sauvegardés puis restaurés automatiquement. | Battery and time safety limits. Your original settings are saved and restored automatically. |
| 5 | Notification « Tâche terminée — Le PC peut se remettre en veille » | Bureau propre, notification en bas à droite | Notification à la fin de la tâche. 100 % local : aucune connexion, aucun compte, aucune télémétrie. | Notification when the task is done. 100% local: no connection, no account, no telemetry. |

Capture bonus (facultative, 6ᵉ) : écran d'accueil, étape 2 « Ce que modifie le mode capot ».

## Logos Store (déjà générés)
`store/03_visuels/icones/store/` : `KeepLid-StoreLogo-300x300.png` (logo 1:1 obligatoire), `1080x1080` et
`2160x2160` (affiches 1:1 facultatives). Régénérer avec :
```bash
pip install cairosvg pillow
python store/03_visuels/generate_icons.py
```

## Icônes MSIX (déjà générées)
`store/03_visuels/icones/msix/` (copiées dans `src/KeepLid.Package/Images/`) : Square44x44 (scale 100–400 +
targetsize 16–256, plated/unplated), Square71x71, Square150x150, Square310x310, Wide310x150, StoreLogo, SplashScreen,
toutes échelles. Icônes de la zone de notification (4 états) : `src/KeepLid.App/Assets/Tray/*.ico`.
