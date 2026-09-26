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
