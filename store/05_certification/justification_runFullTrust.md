# Justification de la capacité restreinte `runFullTrust`

> Partner Center › Soumission › Packages / *Submission options* › « Why do you need the runFullTrust capability? ».
> Coller le texte anglais.

## Texte à coller (EN)
```
KeepLid is a Win32 desktop application (.NET 8 WPF) packaged with MSIX (Desktop Bridge). runFullTrust is required to run a classic desktop process. The app needs it to:

1. Show a notification-area (system tray) icon and menu (Shell_NotifyIcon via System.Windows.Forms.NotifyIcon), which is its main user interface.
2. Call the Win32 power management APIs that have no UWP/WinRT equivalent: SetThreadExecutionState (prevent sleep), PowerGetActiveScheme / PowerReadACValueIndex / PowerReadDCValueIndex / PowerWriteACValueIndex / PowerWriteDCValueIndex / PowerSetActiveScheme (temporarily change and restore the lid close action), GetPwrCapabilities and GetSystemPowerStatus (lid presence, battery level), RegisterPowerSettingNotification (lid open/closed, power plan changes).
3. Register an optional global keyboard shortcut (RegisterHotKey) and enumerate running process names to stop automatically when a user-selected process exits.

The app does NOT request elevation (asInvoker), installs no service or driver, writes nothing outside its own data folder, never connects to the network, and restores every system setting it changes.
```

## Traduction (FR, pour référence)
KeepLid est une application de bureau Win32 (.NET 8 WPF) empaquetée en MSIX. `runFullTrust` est nécessaire pour :
1. l'icône et le menu de la zone de notification (interface principale) ;
2. les API Win32 de gestion de l'alimentation sans équivalent WinRT (`SetThreadExecutionState`, lecture/écriture de
   l'action du capot, état de la batterie, détection du capot, notifications capot / mode d'alimentation) ;
3. le raccourci clavier global optionnel et la liste des noms de processus (arrêt automatique en fin de tâche).
Aucune élévation, aucun service ni pilote, aucune écriture hors de son dossier, aucune connexion réseau, restauration
de tout réglage modifié.
