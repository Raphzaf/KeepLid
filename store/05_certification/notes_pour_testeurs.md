# Notes pour la certification (« Notes for certification »)

> Partner Center › Soumission › *Options de soumission* › **Notes for certification**.
> Les testeurs Microsoft lisent l'anglais : coller le bloc anglais (la traduction française est fournie pour référence).

## Texte à coller (EN)
```
KeepLid is a notification-area (system tray) utility. No account, no sign-in, no network access, no test credentials needed.

WHAT IT DOES
- Keeps Windows awake while a long task runs, using SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED [| ES_DISPLAY_REQUIRED]). It never simulates input.
- Optional "lid mode": temporarily sets the active power plan's "Lid close action" (GUID_SYSTEM_BUTTON_SUBGROUP / GUID_LIDCLOSE_ACTION) to "Do nothing" on AC power (and on battery only after explicit user confirmation), then restores the original value.
- The app never requests elevation. At startup it checks whether the current user may write this setting by writing back the value already in place (no effective change). If Windows refuses (ERROR_ACCESS_DENIED, e.g. standard user or restricted power-plan ACL), the lid mode is replaced by a step-by-step guide that opens Control Panel > Power Options.

HOW TO TEST (about 3 minutes)
1. Launch KeepLid: a 3-step welcome window explains the app. Click Next until "Get started". The KeepLid icon appears in the notification area (it may be in the overflow "^" area).
2. Left-click the icon: it turns blue (awake). Hover it: tooltip "Awake — until turned off". Left-click again: grey (off).
3. Right-click > Keep awake > 30 minutes: tooltip shows the time left.
4. Right-click > Keep awake > "Until a process ends…": check e.g. "notepad" after starting Notepad, click Start. Close Notepad: about 15 s later the app turns off and shows "Task finished".
5. Right-click > Settings…: single settings page. "Restore original power settings now" and "Open the log" are at the bottom.
6. On a laptop: right-click > "Keep working when the lid is closed" (or "Lid closed: setup guide…" if Windows does not allow apps to change it on that machine). On a desktop PC without a lid this item is greyed out, as designed.
7. Right-click > "Quit (restores everything)".

RESTORE GUARANTEE
- The original lid values are saved to %LOCALAPPDATA%\KeepLid\restore.json (package-redirected) BEFORE any change, and restored on turn-off, quit, session end, suspend/resume, unhandled exception, by a watchdog child process if the app is killed, and at the next launch after a crash.
- Check with: powercfg /q SCHEME_CURRENT SUB_BUTTONS LIDACTION

PRIVACY
- 100% local: no network connection at all, no telemetry, no data collection. Only the license status is read through Windows.Services.Store.
- runFullTrust is required because this is a packaged Win32 (WPF) desktop app that uses a NotifyIcon and the Win32 power management APIs (powrprof.dll); see the restricted capability justification.

"Start with Windows" uses the MSIX StartupTask extension and is disabled by default.
```

## Traduction (FR, pour référence)
KeepLid est un utilitaire de la zone de notification. Aucun compte, aucune connexion, aucun identifiant de test.
Il garde Windows éveillé via `SetThreadExecutionState` (sans simuler d'entrée), et en option règle temporairement
l'action de fermeture du capot du mode d'alimentation actif sur « Ne rien faire » (secteur ; batterie seulement après
confirmation), puis restaure la valeur d'origine. Jamais d'élévation : au démarrage, l'app vérifie si l'écriture est
permise en réécrivant la valeur déjà en place ; sinon, un guide pas à pas remplace le mode capot. Procédure de test en
7 étapes ci-dessus, garantie de restauration (sauvegarde préalable, restauration à la sortie, fin de session,
veille, exception, chien de garde, relance), aucune connexion réseau.
