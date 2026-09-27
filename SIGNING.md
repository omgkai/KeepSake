# Signing and notarizing KeepSake

The local app builds are ad-hoc signed. They are not notarized public releases.

Install your **Developer ID Application certificate and its private key** in this Mac's Keychain. Store notarization credentials locally with `xcrun notarytool store-credentials`; do not paste passwords or API keys into chat. An Apple Developer membership alone is not an installed signing identity.

For each architecture, run from the source package:

```sh
python3 release_macos.py '/absolute/path/KeepSake.app' \
  --identity 'Developer ID Application: Your Name (TEAMID)' \
  --keychain-profile 'keepsake-notary' \
  --output '/absolute/path/new-release-directory'
```

This signs a copy, uses hardened runtime, grants JIT only to the .NET engine, uploads the copied app to Apple, requires an Accepted response, staples and validates the ticket, checks Gatekeeper, then creates the final ZIP and SHA-256. It never overwrites the input app. The script has been syntax-checked; the Developer ID/notary service flow still requires execution with your credentials.

Distribute the corresponding source ZIP and third-party notices alongside the app. Follow the asset provenance decisions in THIRD-PARTY-NOTICES.md before public hosting.

References: [Microsoft's macOS deployment guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/macos) and [Apple's notarization workflow](https://developer.apple.com/documentation/security/customizing-the-notarization-workflow).

## Sparkle updates (0.38+)

Sparkle 2.10.0 is pinned by the official SPM archive checksum. build.sh embeds Sparkle.framework; release_macos.py signs its nested helpers and bundles before the app. Keep the update private key in Keychain (account `KeepSake`); never put it in source or release files. SUPublicEDKey is public.

After notarization, name the archives KeepSake-VERSION-macOS-arm64.zip and KeepSake-VERSION-macOS-x86_64.zip. Run create_appcast.py for each with --architecture, --sign-update pointing to Sparkle’s sign_update tool, and --output updates/appcast-ARCH.xml. The tool signs and verifies both the archive and feed. Upload archives and publish the release before committing the new feeds, so clients never see missing downloads. Preserve the signing key in Keychain or your secure credential backup.

The feed URLs use raw.githubusercontent.com/omgkai/KeepSake/main/updates/. Feed and archive signatures are required before extraction. Build numbers must increase with every release. Old 0.37 installations need one manual upgrade to gain Sparkle.
