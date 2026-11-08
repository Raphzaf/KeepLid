# KeepLid 1.0.0 — notes de version

**Date** : à la publication · **Paquet** : 1.0.0.0 · **Architectures** : x64, ARM64 · **Windows** : 10 (2004+) et 11

## Pour la fiche Store (« Nouveautés »)
Voir `store/02_fiche_store/fr-FR.md` et `en-US.md` › *Nouveautés de cette version*.

## Contenu
* Icône de zone de notification : clic gauche = activer / désactiver ; 4 icônes (inactif, actif, actif + capot,
  garde-fou) ; info-bulle avec état, raison et temps restant.
* Menu : garder éveillé 30 min / 1 h / 2 h / 4 h / durée personnalisée / jusqu'à désactivation / jusqu'à la fin d'un
  processus ; écran allumé ; mode capot (ou guide) ; réglages ; quitter (restaure tout).
* Anti-veille par `SetThreadExecutionState` sur un thread dédié, sans simulation d'entrée.
* Mode capot : sauvegarde (`restore.json`) **avant** écriture, AC seulement par défaut, DC après confirmation unique ;
  suivi du changement de mode d'alimentation ; restauration à la désactivation, à la sortie, à la fin de session, à la
  veille / reprise, sur exception, par chien de garde, et au lancement suivant.
* Détection au démarrage du droit d'écriture (option a+ du spike) : guide pas à pas si Windows réserve ce réglage
  aux administrateurs.
* Garde-fous : batterie < 20 %, capot fermé sur batterie > 2 h (réglables). Pas de protection thermique (honnêtement
  documenté).
* Réglages sur une page, accueil en 3 étapes, raccourci global `Ctrl+Alt+Maj+A`, lancement au démarrage (StartupTask,
  désactivé par défaut), journal local avec rotation à 1 Mo.
* Français et anglais selon la langue de Windows.

## Limites connues
* Sur les PC où Windows réserve la modification du mode d'alimentation aux administrateurs (constaté sur Windows
  Server 2022/2025 ; à confirmer selon les éditions client), le mode capot passe par le guide manuel.
* Coupure de courant pendant le mode capot : restauration au lancement suivant de KeepLid.
* PC en veille moderne (S0 Low Power Idle) : voir `docs/TESTS_MANUELS.md` (cas à valider sur matériel réel).

## Vérifications avant soumission
* CI verte : tests unitaires, build, paquets, test d'installation (restauration après crash), WACK.
* Tests manuels `docs/TESTS_MANUELS.md` passés sur au moins un portable Windows 11 x64 et un Windows 10.
