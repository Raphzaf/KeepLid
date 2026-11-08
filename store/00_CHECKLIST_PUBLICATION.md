# Checklist de publication — Microsoft Store (Partner Center)

Ordre recommandé. Chaque case renvoie au fichier prêt à copier-coller.

## A. Avant Partner Center (une fois)
- [ ] Compte développeur Microsoft (individuel ou entreprise) actif — <https://partner.microsoft.com/dashboard>
- [ ] Profil de paiement et formulaire fiscal remplis (*Paramètres du compte › Profil de paiement / Profil fiscal*) — obligatoire pour une app payante
- [x] Éditeur affiché : **RaphZaf** (doit être identique au `PublisherDisplayName` du manifeste)
- [ ] E-mail de support (`<EMAIL_SUPPORT>`) : remplacer ce marqueur dans `store/` (`grep -rn "<EMAIL_SUPPORT>\|<SUPPORT_EMAIL>" store`)
- [ ] Publier les pages de confidentialité / aide : *Settings › Pages › Source : GitHub Actions*, puis lancer le workflow `pages` (dépôt public ou plan GitHub payant ; sinon héberger `store/04_confidentialite/` ailleurs)
- [ ] Vérifier que `https://raphzaf.github.io/KeepLid/privacy-fr.html` et `privacy-en.html` s'ouvrent sans connexion

## B. Créer le produit
- [ ] *Applications et jeux › + Nouveau produit › Application MSIX ou PWA*
- [ ] Réserver le nom **KeepLid** → `store/01_identite/identite.md`
- [ ] Recopier *Gestion des produits › Identité du produit* (Name, Publisher, PublisherDisplayName) dans `identite.md`

## C. Construire le paquet Store
- [ ] Sur un PC Windows avec .NET 8 SDK + Windows SDK : `build/package.ps1` avec les valeurs d'identité → `docs/BUILD_ET_PACKAGING.md` §2
      (ou Visual Studio : *KeepLid.Package › Publier › Associer l'application au Store* puis *Créer des packages d'application*)
- [ ] Lancer le **WACK** sur le `.msix` x64 signé de test → tout « Réussi » (`docs/BUILD_ET_PACKAGING.md` §4)
- [ ] Passer les tests manuels `docs/TESTS_MANUELS.md` (au moins : portable Windows 11 x64, Windows 10, kill + relance)
- [ ] Faire les 5 captures → `store/03_visuels/SPEC_CAPTURES.md`

## D. Soumission (*Démarrer la soumission*)
1. **Tarification et disponibilité** → `store/06_prix/prix_et_essai.md`
   - [ ] Marchés : tous · [ ] Visibilité : publique · [ ] Prix : 3,99 € · [ ] Essai : 7 jours · [ ] Publication : dès certification
2. **Propriétés**
   - [ ] Catégorie : *Utilitaires et outils*
   - [ ] Politique de confidentialité : `https://raphzaf.github.io/KeepLid/privacy-fr.html` (champ unique ; la page renvoie vers l'anglais)
   - [ ] Site web : `https://raphzaf.github.io/KeepLid/` · [ ] Contact support : `<EMAIL_SUPPORT>`
   - [ ] Configuration requise : *Mémoire 4 Go* (facultatif), *Clavier/Souris* : recommandé
   - [ ] Déclarations produit → `store/05_certification/questionnaire_age.md` (bas de page)
3. **Classifications par âge** → `store/05_certification/questionnaire_age.md`
   - [ ] Questionnaire IARC rempli → 3+ / PEGI 3
4. **Packages**
   - [ ] Glisser `artifacts/KeepLid_1.0.0.0_x64_arm64.msixupload`
   - [ ] Vérifier : x64 + ARM64 détectés, Windows 10 Desktop 10.0.19041+, langues en-US / fr-FR, capacité `runFullTrust`
   - [ ] Familles d'appareils : *Windows 10/11 Desktop* uniquement
5. **Descriptions du Store** (une par langue : *Français (France)* et *English (United States)*)
   - [ ] Description, Nouveautés, Fonctionnalités, Description courte, Titre court, Termes de recherche → `store/02_fiche_store/fr-FR.md` / `en-US.md`
   - [ ] Captures (5) + légendes → `store/03_visuels/SPEC_CAPTURES.md`
   - [ ] Logo Store 1:1 300×300 → `store/03_visuels/icones/store/KeepLid-StoreLogo-300x300.png`
   - [ ] Copyright, informations complémentaires
6. **Options de soumission**
   - [ ] *Notes for certification* → `store/05_certification/notes_pour_testeurs.md`
   - [ ] *Restricted capabilities › runFullTrust* → `store/05_certification/justification_runFullTrust.md`
7. - [ ] **Soumettre au Store** — certification habituellement en 1 à 3 jours ouvrés

## E. Après publication
- [ ] Installer depuis le Store sur un PC propre, essai visible (« Essai gratuit : encore 7 jour(s) »)
- [ ] Vérifier le lien Store dans la fiche et la fenêtre d'achat à l'expiration (compte de test ou machine de test)
- [ ] Répondre aux avis → `store/07_support/reponses_types_avis.md` ; FAQ → `store/07_support/FAQ.md`
- [ ] Maintenance hebdomadaire (< 1 h) : lire les avis et le tableau *Santé* (plantages) de Partner Center, répondre aux e-mails

## Mise à jour (versions suivantes)
- [ ] Incrémenter la version (`1.0.1.0`, 4ᵉ nombre = 0), mettre à jour `CHANGELOG.md` et `store/08_release/`
- [ ] `build/package.ps1 -Version 1.0.1.0 …`, CI verte, WACK, nouvelle soumission (seuls *Packages* et *Nouveautés* changent)
