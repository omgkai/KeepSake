# PKHeX updates

The **PKHeX upstream updates** GitHub workflow checks the latest stable release of `kwsch/PKHeX` daily at 12:23 UTC. It can also be run manually from Actions. It ignores unreleased commits and prereleases, and never downgrades the bundled version.

`Source/Vendor/PKHeX-UPSTREAM.json` records the bundled Core release and commit. A newer release is copied into a version-specific branch and proposed as a draft PR. An existing open PR is reused and checked without overwriting maintainer edits; a closed PR is not automatically reopened. A pushed branch whose PR creation failed can be recovered on the next run.

The update includes Core, build properties, its license, upstream README, icon and build revision. Auto-Legality stays pinned so an incompatible API change fails visibly. Artwork, translations and extracted Windows data are not automatically refreshed; their existing provenance remains applicable.

Compatibility runs in a separate job with read-only repository access: Core/Auto-Legality/bridge compilation, abilities across formats, Pokédex round trips and undo, party/box operations, growth/training and Alpha generation. The result is attached to the exact candidate commit as **PKHeX compatibility**, including when GitHub does not automatically start PR workflows for a bot-created PR. PR-triggered checks also cover later maintainer edits. Failure or missing checks require review; no fallback marks them successful.

The checks use synthetic data. Native Mac builds, additional game-data changes and relevant real saves still need review before release. This workflow does not merge PRs, change app versions, access signing credentials, publish releases or alter Sparkle feeds. Release signing and notarization remain a separate, deliberate step.

Repository Actions must allow GitHub Actions to create pull requests. The updater requests only contents/PR write access; compilation has no write credentials, and a separate reporting job can only write commit statuses. Disable the scheduled workflow from Actions to stop checks. GitHub may disable scheduled workflows after extended repository inactivity.
