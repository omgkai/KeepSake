# KeepSake 0.41

## PKHaX mode

Enable **Settings → Engine → PKHaX mode** for unrestricted editing. Automatic Pokémon, move-picker and hover legality checks are disabled and shown as **Unchecked**. Auto-Legality and move suggestions remain available as deliberate manual actions.

- Choose any ability supported by the file format in Generation 4 and later.
- Edit raw form IDs and stored party levels/stats under **Stats → PKHaX · Raw values**.
- Max EVs fills every stat to its individual maximum in PKHaX mode, without enforcing the standard total.
- Format limits still apply. The game can recalculate party stats; box formats may not retain them.
- The preference is saved. Other already-open KeepSake windows keep their mode until reopened. Turn PKHaX off to resume automatic checks.

## Keeping the engine current

A daily GitHub workflow now checks official stable PKHeX releases and proposes Core updates as draft pull requests, with separate compatibility checks. It does not merge changes or publish an app update automatically. Auto-Legality and the native interface are reviewed before a new release.

Apple silicon and Intel downloads include the runtime. Install in Applications; existing Sparkle updates remain supported.
