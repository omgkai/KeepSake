KeepSake 0.38 adds in-app downloads, installation and relaunch through Sparkle.

- **KeepSake → Check for Updates** now downloads, verifies and installs the update instead of opening a web page.
- Daily automatic checks and optional automatic download/install are available in **Settings → Updates & Backup**. Automatic downloads install when the app quits.
- Unsaved work in every save window can cancel quitting. You can export your changes and retry the ready update later.
- Separate Apple Silicon and Intel feeds use signed update metadata and signed archives, verified before extraction. Release apps and installer helpers are Developer ID signed and Apple notarized.

**One-time upgrade:** 0.37 and earlier still use the old update checker. Download the matching 0.38 ZIP, quit KeepSake and replace your app once. Future releases can install directly in KeepSake.

Requires macOS 14 or later. Journals, settings and saves are kept outside the app bundle and remain in place during updates.
