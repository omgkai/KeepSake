# Notices

This unofficial native interface is not affiliated with or endorsed by the PKHeX project, Nintendo, GAME FREAK, or The Pokémon Company.

PKHeX.Core is by Kaphotics and contributors, licensed GPL-3.0-or-later. Its bundled release and exact source revision are recorded in [Source/Vendor/PKHeX-UPSTREAM.json](Source/Vendor/PKHeX-UPSTREAM.json); its source is under Source/Vendor/PKHeX.Core and its license under Source/Vendor/LICENSE. Core update proposals do not update separate artwork or extracted game data.

The SpriteName utility and the original asset/data baseline are from PKHeX commit 08c27668d28a83ad4b04140436a384d4155ed134, https://github.com/kwsch/PKHeX. References below to the pinned upstream asset commit refer to this original revision. The SwiftUI interface and bridge are provided under GPL-3.0-or-later as part of this derivative project; see LICENSE.

The regular Pokémon sprites and artwork are copied unchanged from PKHeX.Drawing.PokeSprite/Resources/img/Big Pokemon Sprites and Artwork Pokemon Sprites at the same upstream commit. Their source collection and attribution are retained in Source/Vendor/UPSTREAM-README.md. Pokémon characters and related artwork belong to their respective rights holders. Version 0.12 additionally includes 1,777 unchanged images from Big Shiny Sprites and Artwork Shiny Sprites at the same commit. Source/Assets/shiny-sources.json records their paths. The separate Legends: Arceus sprite collections are not bundled; the user-supplied Gen 9 sheets take precedence as described below.

Ribbon and mark icons are copied unchanged from PKHeX.Drawing.Misc/Resources/img/ribbons at the same pinned commit. Their names and rank variants follow the upstream RibbonSpriteUtil rules.

Box wallpapers are copied unchanged from PKHeX.Drawing.Misc/Resources/img/box at the same pinned commit. The bridge adapts the upstream WallpaperUtil mapping to choose the game-specific image.

The regular and shiny Generation 9 icon sheets are by **Ezerart**: [Pokémon Gen 9 Icon sprites (3DS Style)](https://www.deviantart.com/ezerart/art/Pokemon-Gen-9-Icon-sprites-3DS-Style-944211258) and [Shiny Pokémon Gen 9 Icon sprites (3DS Style)](https://www.deviantart.com/ezerart/art/Shiny-Pokemon-Gen-9-Icon-sprites-3DS-Style-944778082). Their 284 non-empty cells are extracted without recoloring, scaling, or transparency changes. The original sheets and species/form mappings are retained in Source/Assets/Gen9SpriteSheet. These supplied art assets are distinct from the GPL application code; no additional artwork license is asserted. They replace the Gen 9 artwork fallback and add supplied regional/form icons. The Shiny toggle selects the supplied shiny variants.

The .NET runtime is by Microsoft and contributors. Its included LICENSE.txt and THIRD-PARTY-NOTICES.TXT accompany the bundled runtime in the app's Contents/Helpers directory. Runtime source and licensing are available at https://github.com/dotnet/runtime .

Game logo artwork in Source/Assets/GameLogos is copied unchanged from official Pokémon game sites (Arceus, Sword/Shield, BDSP and Z-A) and Bulbagarden Archives (remaining main-series games, side-game logos and the Pokémon wordmark). Early English title lockups in the SwiftUI renderer combine the original wordmark with native text; they are not original logo artwork. Source page and image URLs are retained in sources.json. Pokémon logos are copyrighted/trademarked property of their respective rights holders; they are used to identify the loaded save's game. No GPL or other additional license is asserted for these separate artwork assets. This app remains unofficial.

Move type icons are copied unchanged from PKHeX.Drawing.Misc/Resources/img/types/square at the same pinned upstream commit and bundled in Source/Assets/MoveTypes.

KeepSake's item and Poké Ball icons are unchanged upstream assets from PKHeX.Drawing.PokeSprite/Resources/img/Big Items, Artwork Items and ball at the pinned commit. The four legal Gen 7 wardrobe payloads in Source/Engine/Fashion are copied unchanged from PKHeX.WinForms/Resources/byte. Their upstream GPL source is included in the source distribution. The navy journal icon is drawn by Source/Packaging/DrawIcon.swift; it is separate from the supplied Pokémon sprite sheets. KeepSake is an unofficial application, not an official Pokémon product.

The 104 PNGs in Source/Assets/HisuiItems are from **lichen**’s [Legends: Arceus item sprites](https://eeveeexpo.com/resources/1287/), supplied in the hisui items folder. They are bundled byte-for-byte unchanged, with file hashes and item/ball mappings in manifest.json. These separate fan-art assets are credited to their creator and are not relicensed under the application’s GPL license. Original transparency and white outlines are preserved. The accompanying items.txt is fan-game configuration data and was not used to set save rules, prices, or item legality. The app continues to use PKHeX.Core for those values. Unmapped artwork is retained in the source folder without being assigned to unrelated items.

The 68 PNGs in Source/Assets/PaldeaItems are from **lichen**’s [Scarlet and Violet item sprites](https://eeveeexpo.com/resources/1288/), supplied in the paldea items folder, and are retained unchanged with hashes in manifest.json. Matching Scarlet/Violet item IDs use these images; the supplied generic TM-material and legendary-treat artwork covers the corresponding groups. The alternate reduced-color Clear Amulet and unmatched artwork are retained without overriding the primary matching image. These separate fan-art assets are credited to their creator and are not relicensed under the application’s GPL license. Original transparency and white outlines are preserved.

Fashion catalog labels and indices for Legends: Arceus and Sword/Shield are factual item-name data from foohyfooh/PKHeXPluginPile, commit fb76c3c52c6386e21ec75d9c5f702c6d38a27477, PluginPile.FashionEditor/Resources (https://github.com/foohyfooh/PKHeXPluginPile). The plugin executable and its editor source are not bundled. No additional license is asserted for its separate game-label data. `Source/Engine/Fashion/provenance.json` records source paths and hashes. Ownership adapters and category illustrations are new KeepSake code; these illustrations are not original in-game garment thumbnails.

Scarlet/Violet clothing ID-to-name facts were transcribed from FrostGiratina's “Fasion Guide ScVi.txt”, linked in https://projectpokemon.org/home/forums/topic/63383-sv-fashion-block-research/ . The resulting sv_names.json retains short item names and IDs, including paired uniform variants. Unknown items remain labeled by ID. Unlock rules come from the pinned PKHeX.Core, not the name reference. Pokémon item names remain property of their respective rights holders.

Version 0.16 includes 41 unchanged donut images from PKHeX.Drawing.Misc/Resources/img/donut at the pinned upstream commit. Source/Assets/Donuts/sources.json retains paths and hashes. Honey-tree and Alola cell/sticker location labels in Source/Engine/GameData are extracted from the pinned PKHeX.WinForms SAV_HoneyTree.cs and SAV_ZygardeCell.cs dialogs. The corresponding upstream GPL attribution and license apply. Pokémon artwork remains the property of its respective rights holders.

## Auto-Legality Mod

**Auto-Legality Mod** was created by **Archit Date (architdate)**, with Kaphotics and other contributors: [original project](https://github.com/architdate/PKHeX-Plugins). KeepSake bundles the [santacrab2-maintained fork](https://github.com/santacrab2/PKHeX-Plugins).
Commit: 90410f2681a0a72680d12280a1e0f14715e67dff
MIT License, Copyright (c) 2018 Archit Date; license included. Library sources unmodified; csproj uses the bundled PKHeX Core 26.8.26 project instead of NuGet, with upstream build properties embedded.

Only the Core.AutoMod library is bundled; no Windows plugin UI or remote service is used.

```text
MIT License

Copyright (c) 2018 Archit Date

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

```

## User-supplied trainer badge art (0.19)

**JcFerggy** — [16x16 Pokémon Badge Sprites: Gen 1–6](https://www.deviantart.com/jcferggy/art/16x16-Pokemon-Badge-Sprites-Gen-1-6-544204402) (`d9006sy`), and **ProfessorMorDBG** — [Paldea Badges demake large](https://www.deviantart.com/professormordbg/art/Paldea-Badges-demake-large-1142694862) (`diwbwzy`). The original PNG sheets are retained unchanged in `Source/Assets/Badges`; the manifest identifies the 68 regions used by the native renderer. Supplied fan artwork is not relicensed by KeepSake’s GPL source license. Galar uses a native seal fallback, not artwork attributed to either sheet.

Festival Plaza phrase labels in Source/Engine/GameData/festivalPhrases.json are extracted from the pinned upstream PKHeX.WinForms SAV_FestivalPlaza.cs. The upstream GPL attribution and license apply. New typed Pokéathlon, Secret Base, Festival Plaza and Pokédex adapters use the unchanged pinned PKHeX.Core structures; the Windows dialog source supplied the field layout reference.

Pokémon HOME HD portraits are copied unchanged from PokeAPI/sprites, directory sprites/pokemon/other/home, pinned commit a13b1f4ccd77f35fd1370d2db5f0051221e9683f (https://github.com/PokeAPI/sprites). The 3,260 PNGs retain their original pixels and alpha. Source/Assets/Portraits/SOURCE.json records the source paths and Git blob hashes; LICENCE.txt retains the repository notice identifying image copyright as The Pokémon Company. No GPL license is asserted for these separate artwork assets. Form mapping also uses PokeAPI's public pokemon.csv and pokemon_forms.csv (https://github.com/PokeAPI/pokeapi/tree/master/data/v2/csv) alongside the pinned PKHeX form labels. HD portrait availability is independent of whether a form or shiny state is legal in a particular save.

## Ability descriptions

English game flavor text is bundled from the PokeAPI data project: https://github.com/PokeAPI/pokeapi/blob/master/data/v2/csv/ability_flavor_text.csv (retrieved September 26, 2026). Pokémon game text remains the property of its respective rights holders. KeepSake selects descriptions by generation where available; unavailable game variants use the newest available entry.

## Game portrait artwork (0.31)

`Source/Assets/GamePortraits/SV` contains 933 unchanged Scarlet/Violet images from PokeAPI/sprites at pinned commit a13b1f4ccd77f35fd1370d2db5f0051221e9683f, directory sprites/pokemon/versions/generation-ix/scarlet-violet. `SOURCE.json` records original paths, sizes and Git blob hashes, verified at download. `LICENCE.txt` preserves the repository artwork notice. This collection has no shiny assets: KeepSake falls back to the matching shiny HOME portrait, then the shiny pixel sprite.

`Source/Assets/GamePortraits/PLA` contains 632 unchanged Arceus normal/shiny assets from PKHeX.Drawing.PokeSprite at pinned commit 08c27668d28a83ad4b04140436a384d4155ed134. Its SOURCE.json records original paths and SHA-256 hashes. Transparent margins are trimmed only while rendering. Character artwork remains the property of its respective rights holders; this is not a new redistribution license. Missing exact-form or gender artwork falls back to HOME. The Game portraits option uses Arceus art for PLA saves/standalone PA8 and Scarlet/Violet art otherwise.

## Interface translations

Common interface translations in Source/Assets/Localization are adapted from PKHeX.WinForms Resources/text/lang_*.txt at the bundled PKHeX revision, under GPL-3.0-or-later. Additional KeepSake navigation translations are in keepSake.tsv. Untranslated interface text falls back to English.

## Sparkle 2.10.0

In-app updates use Sparkle, https://github.com/sparkle-project/Sparkle/tree/2.10.0. Its MIT license and bundled third-party notices are reproduced in Source/Packaging/Sparkle-LICENSE.txt and shipped as Sparkle-LICENSE.txt. The Swift package binary is pinned to the upstream archive checksum.

## Held-item descriptions

English item flavor text and legacy item mappings are from [PokeAPI](https://github.com/PokeAPI/pokeapi/tree/master/data/v2/csv): item_flavor_text.csv, item_names.csv and item_game_indices.csv, retrieved September 27, 2026. The compact offline catalog is in Source/Engine/GameData/itemDescriptions.json. Descriptions prefer the loaded game, with an explicitly labeled reference fallback when unavailable. Pokémon game text belongs to its respective rights holders.
