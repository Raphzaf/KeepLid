# Changelog

Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) ; versions [SemVer](https://semver.org/lang/fr/)
(paquet MSIX : `X.Y.Z.0`).

## [1.0.0] — non publiée

### Ajouté
- Icône de zone de notification (4 états), clic gauche pour activer / désactiver, info-bulle avec état et temps restant.
- Garder éveillé : 30 min, 1 h, 2 h, 4 h, durée personnalisée, jusqu'à désactivation, jusqu'à la fin d'un processus
  (préréglages : claude, node, python, codex, cursor, ollama, code).
- Anti-veille via `SetThreadExecutionState` sur un thread dédié ; option « Garder l'écran allumé ».
- Mode capot fermé : sauvegarde `restore.json` avant écriture, secteur par défaut, batterie après confirmation unique,
  suivi des changements de mode d'alimentation, restauration à la sortie / fin de session / veille / exception /
  relance, processus chien de garde.
- Détection au démarrage du droit d'écrire l'action du capot ; guide pas à pas sinon (résultat du spike Phase 0).
- Garde-fous : batterie faible (20 %), durée max capot fermé sur batterie (2 h).
- Réglages sur une page, écran d'accueil en 3 étapes, raccourci global `Ctrl+Alt+Maj+A`, lancement au démarrage
  (StartupTask MSIX, désactivé par défaut), journal local avec rotation à 1 Mo.
- Interface en français et en anglais.
- Essai gratuit Store de 7 jours, puis achat unique.
- Paquets MSIX x64 + ARM64, `.msixbundle`, `.msixupload` ; CI : tests, paquets, test d'installation, WACK.
