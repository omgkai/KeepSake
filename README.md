# KeepSake

**Every companion has a story.**

KeepSake is a native macOS Pokémon save editor and personal journal, powered by PKHeX.Core and offline Auto-Legality. It combines game-aware editing with customizable journals, teams, trainer cards, themes, and Pokémon artwork.

Requires macOS 14 or later. Apple Silicon and Intel builds include their own runtimes; Wine and a separate .NET installation are unnecessary.

## Downloads and updates

Published builds appear on the [Releases page](https://github.com/omgkai/KeepSake/releases). If no download is listed yet, the first signed build is still being prepared.

KeepSake checks public GitHub releases daily while open. Choose **KeepSake → Check for Updates** for a manual check, or disable automatic checks under **Settings → Updates & Backup**. Updates open the release page for you to download; KeepSake does not silently replace a running app. No saves or trainer details are sent with update checks.

## Your adventures

- Open a save or Pokémon file, or drop it into the editor. Explore game-specific sample workspaces without opening a personal file.
- Edit Pokémon, boxes, trainer details, inventory, Pokédex, Mystery Gifts and game-specific collections. Available tools follow the loaded game and format.
- Export an edited copy; your original save stays intact. Set pending Pokémon edits to a slot before exporting the save.
- Capture your current party as a journal team. Add memories, favorites, custom names and covers without changing game data.
- Choose sprites, HOME portraits or game portraits, plus themes and custom colors.

The editor uses one save workspace. About and Support open in separate windows so help can stay beside your work. Independent simultaneous save workspaces are not supported.

## Backup and restore

**Settings → Files & Startup** manages original-save backups and save discovery. **Updates & Backup** lets you choose a destination, including a folder in iCloud Drive, and create a dated backup containing journal pages, teams, basic appearance preferences and existing original-save backups.

Cloud transfer is managed by macOS. KeepSake confirms that files were written locally, not that they have finished uploading. Keychain is for credentials, not save storage. Backups do not include unsaved editor changes or arbitrary files elsewhere on your Mac.

To restore your journal, import `Journal.json` from My Journal; entries and teams merge using their modification dates. For a save, copy the appropriate `.bak` file out of `Original Save Backups` and open that copy in KeepSake. `RESTORE.txt` accompanies each backup. `Appearance.plist` records basic preferences for reference; it is not automatically imported.

## Help

Choose **Help → KeepSake Support** for backup tools, update checks and a link to [Issues](https://github.com/omgkai/KeepSake/issues). Copy App Details includes only the app version, macOS version and architecture. Avoid posting personal saves or trainer details in public reports.

## Build

Requires Swift 6 tooling, the macOS SDK and .NET SDK 10.

```sh
KEEPSAKE_ARCH=arm64 KEEPSAKE_APP_PATH="$PWD/KeepSake-AppleSilicon.app" bash build.sh
KEEPSAKE_ARCH=x86_64 KEEPSAKE_APP_PATH="$PWD/KeepSake-Intel.app" bash build.sh
```

Use distinct output paths. The build script writes app bundles and applies a local ad-hoc signature. [SIGNING.md](SIGNING.md) explains Developer ID signing and notarization; credentials belong in Keychain. [FEATURE-COVERAGE.md](FEATURE-COVERAGE.md) documents implementation coverage and native differences. Windows UI layout equivalence, LiveHeX and Windows plugins are outside this release's scope. The interface is English with ten-language game-data catalogs.

## Credits and license

PKHeX is by Kaphotics and contributors. Auto-Legality Mod is by its respective contributors. KeepSake is an unofficial project distributed under GPL-3.0-or-later; see [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Pokémon and artwork belong to their respective owners. The source license does not relicense separate artwork.
