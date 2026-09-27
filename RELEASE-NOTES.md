# KeepSake 0.42 — Save export hotfix

Fixes a critical Switch save export issue: PKHeX recognition decrypts its input buffer in place. KeepSake now validates a separate copy, preserving the encrypted bytes written to disk. Automatic backups also retain untouched original bytes.

- Legends: Arceus exports default to **main**, with no added extension. Other games retain their save filename.
- Export guidance explains using a separate folder and retaining the game’s filename.
- Export verifies the save format and checksums; container flags follow PKHeX’s rules for the chosen extension.

**Update before exporting Switch saves or creating new automatic backups.** Previously affected exports/backups are not repaired by installing this update. Keep the original files; an affected Arceus export was recovered without altering its game data by restoring the missing encryption layer and verifying its existing integrity hash.

Validation: both native Mac builds compile. A focused Arceus regression check verified backup and unedited-export byte identity, successful reopening, game recognition and checksums. This is not an in-game restore test. No user save is included in source or downloads.
