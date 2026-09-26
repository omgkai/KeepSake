# Portrait manifest maintenance

The app uses the checked-in manifest and PNG files; it never requests artwork online.

`Source/Assets/Portraits/SOURCE.json` identifies the pinned PokeAPI sprites revision and original Git blob hashes. Image rights and upstream notices are recorded in THIRD-PARTY-NOTICES.md.

To regenerate form identities against the bundled PKHeX core, run the .NET project here and redirect its JSON output into `forms.json`, then run `python3 map.py`. Build first and execute the compiled DLL directly to keep build messages out of the JSON. The script uses the already-bundled PNGs and the included PokeAPI `pokemon.csv` / `pokemon_forms.csv` snapshots from https://github.com/PokeAPI/pokeapi/tree/master/data/v2/csv (retrieved September 26, 2026). It writes the app manifest and an `unmatched.json` diagnostic. Missing exact forms intentionally retain their pixel sprite.

Then compile and run `Tests/PortraitResourceChecks.swift` with `Source/Native/Sources/PortraitResources.swift`, passing the Portraits resource directory as the first argument. Generation and tests require developer toolchains; app users do not.
