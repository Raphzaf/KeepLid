# FAQ — KeepLid

Version publiée en ligne : `store/04_confidentialite/aide.html` (FR) et `help.html` (EN).

---

### 1. Pourquoi l'écran s'éteint-il quand je ferme le capot, alors que KeepLid est actif ?
C'est normal et matériel : fermer le capot coupe l'écran intégré (capteur de capot / charnière). KeepLid empêche la
**mise en veille**, pas l'extinction de l'écran interne. Le PC continue de travailler : votre agent IA, votre
compilation ou votre téléchargement avancent. Rouvrez le capot pour voir le résultat.

### 2. Le menu propose « Capot fermé : guide de réglage… » au lieu d'une case à cocher. Pourquoi ?
Sur certains PC (souvent ceux gérés par une entreprise, ou selon la configuration de Windows), Windows réserve la
modification du mode d'alimentation aux administrateurs. KeepLid ne demande **jamais** de droits administrateur ; il
le détecte au démarrage et vous propose un guide pour régler vous-même « Lorsque je ferme le capot » sur
« Ne rien faire » (30 secondes), puis vous rappelle de le remettre à la fin. L'anti-veille fonctionne normalement.

### 3. Est-ce que ça vide ma batterie ?
Un PC éveillé consomme plus qu'un PC en veille, surtout capot fermé avec une tâche lourde. C'est pourquoi :
le mode capot n'agit par défaut que **sur secteur** ; sur batterie, il demande votre confirmation ; KeepLid se
désactive tout seul sous **20 %** de batterie et après **2 h** capot fermé sur batterie (réglables).

### 4. Est-ce dangereux pour mon portable ?
Un portable allumé doit pouvoir évacuer sa chaleur. **Ne le laissez jamais allumé dans un sac fermé** ni sur une
surface molle (lit, canapé). KeepLid ne lit pas les températures (cela exigerait des droits administrateur) : ses
garde-fous portent sur la batterie et la durée. Windows et le matériel gardent leurs propres protections thermiques.

### 5. KeepLid modifie-t-il mes réglages Windows ?
Uniquement le réglage « Lorsque je ferme le capot » du mode d'alimentation actif, et seulement quand vous activez le
mode capot. La valeur d'origine est enregistrée **avant** la modification et remise automatiquement : à la
désactivation, en quittant, à la fermeture de session, à l'arrêt, à la mise en veille et, après un plantage, au
lancement suivant. Si vous changez vous-même ce réglage entre-temps, KeepLid respecte votre nouveau choix.

### 6. Mon PC a planté (ou j'ai tué KeepLid) pendant le mode capot. Mon réglage est-il perdu ?
Non. Un petit processus « chien de garde » restaure le réglage si KeepLid est tué. Si tout s'est arrêté d'un coup
(coupure de courant), la restauration a lieu au **prochain lancement** de KeepLid (activez « Lancer au démarrage de
Windows » pour qu'elle soit automatique). Vous pouvez aussi cliquer sur Réglages › « Restaurer les réglages
d'alimentation d'origine maintenant ». Vérification manuelle : `powercfg /q SCHEME_CURRENT SUB_BUTTONS LIDACTION`.

### 7. KeepLid envoie-t-il des données ? Faut-il un compte ?
Non. KeepLid est 100 % local : aucune connexion internet, aucune télémétrie, aucun compte. Voir la politique de
confidentialité.

### 8. Comment garder le PC éveillé jusqu'à la fin de mon agent IA ?
Clic droit sur l'icône › Garder éveillé › « Jusqu'à la fin d'un processus… », cochez le processus (par exemple
`claude`, `node`, `python`, `codex`, `cursor`, `ollama`, `code`) puis Démarrer. KeepLid se coupe environ 15 secondes
après la fin du dernier processus coché et affiche « Tâche terminée ».

### 9. KeepLid simule-t-il des mouvements de souris ?
Non. Il utilise l'API officielle de Windows (`SetThreadExecutionState`) qui indique au système qu'une tâche est en
cours. Aucune touche ni mouvement de souris n'est simulé, et votre verrouillage de session reste actif si vous
n'activez pas « Garder l'écran allumé ».

### 10. Comment désinstaller KeepLid proprement ?
Quittez KeepLid (clic droit › « Quitter (restaure tout) ») : tous les réglages sont remis. Puis Paramètres Windows ›
Applications › KeepLid › Désinstaller. Ses fichiers (préférences, journal) sont supprimés avec l'application ; rien
n'est laissé dans le registre.
