# Prix et essai

> Partner Center › Soumission › **Tarification et disponibilité**.

| Champ | Valeur |
|---|---|
| Marchés | **Tous les marchés** (« Rendre disponible dans tous les marchés actuels et futurs ») |
| Visibilité | Public, découvrable dans le Store |
| Prix de base | **3,99 €** (palier de prix EUR le plus proche ; Partner Center convertit automatiquement pour les autres devises) |
| Type d'achat | **Achat unique**, aucun achat intégré, aucun abonnement |
| Essai gratuit | **7 jours** (« Essai gratuit : 7 jours ») |
| Date de publication | « Dès que la soumission est certifiée » (ou manuelle si vous préférez contrôler le lancement) |
| Ventes / promotions | aucune au lancement |
| Familles d'appareils | Windows 10/11 Desktop uniquement |
| Organisation | ☐ Ne pas cocher « Disponible pour les organisations via le Microsoft Store pour Entreprises » (option retirée selon les comptes) |

## Comment l'essai fonctionne dans l'app
* L'app lit la licence locale via `Windows.Services.Store.StoreContext.GetAppLicenseAsync()` (aucune connexion faite par l'app).
* Pendant l'essai : le menu affiche « Essai gratuit : encore N jour(s) » ; toutes les fonctions sont disponibles.
* Essai expiré : l'activation est bloquée et le menu propose « Essai terminé — acheter KeepLid… » (fenêtre d'achat du
  Store via `RequestPurchaseAsync`). **La restauration des réglages, les réglages et « Quitter » restent toujours
  disponibles.**
* En cas d'erreur de lecture de la licence, l'app considère la licence valide (ne jamais bloquer un client qui a payé).

## Revenus
* Commission Microsoft Store pour les applications : voir l'accord développeur en vigueur dans Partner Center
  (à vérifier au moment de la publication, les conditions évoluent).
* Coût de fonctionnement de KeepLid : **0 €** (aucun serveur ; pages de confidentialité/aide hébergées gratuitement
  sur GitHub Pages).
