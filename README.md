# KeepSake

**Every companion has a story.**

KeepSake is a native macOS Pokémon save editor and personal journal, powered by PKHeX.Core and offline Auto-Legality. It combines game-aware editing with customizable journals, teams, trainer cards, themes, and Pokémon artwork.

Requires macOS 14 or later. Apple Silicon and Intel builds include their own runtimes; Wine and a separate .NET installation are unnecessary.

## Downloads and updates

Published builds appear on the [Releases page](https://github.com/omgkai/KeepSake/releases). If no download is listed yet, the first signed build is still being prepared.

KeepSake 0.38 and later use Sparkle to download, verify, install and relaunch updates inside the app. Choose **KeepSake → Check for Updates**, then **Install Update**. Settings → Updates & Backup offers daily automatic checks and optional automatic download/install on quit. Every open save workspace can cancel quitting to protect unsaved edits. Updates use signed architecture-specific feeds and signed archives; no saves or trainer details are sent.

**One-time migration:** 0.37 and earlier only open the release page. Download and replace the app once to install the latest version; subsequent updates install in-app. If macOS requests authorization to replace a copy in Applications, complete the system prompt.

## Your adventures

- Open a save or Pokémon file, or drop it into the editor. Explore game-specific sample workspaces without opening a personal file.
- Edit Pokémon, boxes, trainer details, inventory, Pokédex, Mystery Gifts and game-specific collections. Available tools follow the loaded game and format.
- Export an edited copy; your original save stays intact. Set pending Pokémon edits to a slot before exporting the save.
- Capture your current party as a journal team. Add memories, favorites, custom names and covers without changing game data.
- Choose sprites, HOME portraits or game portraits, plus themes and custom colors.

Choose **File → New KeepSake Window (⌘N)** to open another independent save workspace. Each window has its own save, editor and Undo history. Journals and appearance preferences are shared. Closing a window only closes that workspace; unsaved changes require confirmation. Dragging Pokémon between separate save windows is not supported; export and open a Pokémon file instead. About and Support also open separately.

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

Use distinct output paths. The build script writes app bundles and applies a local ad-hoc signature. [SIGNING.md](SIGNING.md) explains Developer ID signing and notarization; credentials belong in Keychain. [FEATURE-COVERAGE.md](FEATURE-COVERAGE.md) documents implementation coverage and native differences. Windows UI layout equivalence, LiveHeX and Windows plugins are outside this release's scope. Settings includes all ten PKHeX interface languages, independently of the game-data catalog language. Common editor labels and navigation reuse PKHeX translations; untranslated specialized tools and guidance fall back to English.

## Credits and license

Made with love by **Kai White**.

- **PKHeX:** [Kaphotics and contributors](https://github.com/kwsch/PKHeX).
- **Auto-Legality Mod:** [Archit Date (architdate)](https://github.com/architdate/PKHeX-Plugins), Kaphotics and contributors; bundled [santacrab2 fork](https://github.com/santacrab2/PKHeX-Plugins), MIT license.
- **Legends: Arceus and Scarlet/Violet item sprites:** **lichen**, via [Arceus collection](https://eeveeexpo.com/resources/1287/) and [Scarlet/Violet collection](https://eeveeexpo.com/resources/1288/).
- **Paldea badges:** **ProfessorMorDBG**, [Paldea Badges demake large](https://www.deviantart.com/professormordbg/art/Paldea-Badges-demake-large-1142694862).
- **Gen 1–6 badges:** **JcFerggy**, [16x16 Pokémon Badge Sprites](https://www.deviantart.com/jcferggy/art/16x16-Pokemon-Badge-Sprites-Gen-1-6-544204402).
- **Gen 9 icon sprites:** **Ezerart**, [normal](https://www.deviantart.com/ezerart/art/Pokemon-Gen-9-Icon-sprites-3DS-Style-944211258) and [shiny](https://www.deviantart.com/ezerart/art/Shiny-Pokemon-Gen-9-Icon-sprites-3DS-Style-944778082).
- **Portrait collections:** PKHeX contributors and [PokeAPI/sprites](https://github.com/PokeAPI/sprites); Pokémon artwork belongs to its respective rights holders.
- **In-app updates:** [Sparkle contributors](https://github.com/sparkle-project/Sparkle), MIT license.

 KeepSake is an unofficial project distributed under GPL-3.0-or-later; see [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Pokémon and artwork belong to their respective owners. The source license does not relicense separate artwork.
