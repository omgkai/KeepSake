# Feature coverage — 0.37

Current release differences: independent save windows (⌘N), separate engine and Undo state per window, shared journals and appearance, per-window close protection and multi-window quit protection; Noble beside Alpha in Stats for PA8; all ten PKHeX language choices with common labels/navigation translated and English fallback for specialized guidance. Port Coverage was removed from the app; this developer inventory remains. Windows-only plugins and LiveHeX are outside the agreed release scope.

The following inventory and dated notes describe earlier implementation milestones. They are not a claim that every Windows dialog has identical native behavior. Public builds since 0.36 have Developer ID signing and Apple notarization; real-save/game-load testing remains user-owned, and physical Intel testing remains open.

# Historical inventory — 0.35 RC1

The request is a SwiftUI Mac app with all the settings of Windows PKHeX. **That full-parity goal is still open.** RC1 is an offline test candidate. It is not a claim of exact Windows UI/settings equivalence or a notarized public release.

## Current implementation

| Area | Coverage |
|---|---|
| Native app | SwiftUI on Apple Silicon and Intel (Rosetta engine QA; physical Intel QA open); native menus/file dialogs; 15 game-inspired themes and shared named color presets, custom colors and automatic loaded-save palettes, subtle backgrounds, light/dark/system appearance; organized Colors/Display/Motion sections, pixel sprites or HD HOME portraits, individual storage-slot opacity/outlines and background tint |
| Game identity | 47 bundled images; main-series, individual sequels and side games, paired identities for ambiguous formats. Native English wordmark/title combinations for early and a few side titles |
| Sample workspaces | Searchable choice of 39 main-series game variants; practice buffers, not playable saves |
| Save engine | Unmodified PKHeX.Core 26.08.26, local process, bundled runtime |
| Pokémon fields | Curated Main/Met/Stats/Moves/Trainer/Cosmetics tabs; searchable names; Shiny/Egg/Alpha switches; colored base-stat/IV/effort/total grid with grit and PKHeX IV-potential stars; guided extras in More; all exposed scalar fields under Open All Fields; obvious no-op setters disabled |
| Boxes and party | Six-column storage grid with native slot buttons and held-item artwork; browse, import, atomic Set to Slot including right-click destinations and typed edits, clear, rename; staged upstream box sorting/modification/deletion actions; Move / Swap menu, box names/game wallpapers/order with undo; full-fill backgrounds and persistent per-game/box Mac wallpaper gallery; drag/drop implemented but gesture QA remains open |
| Trainer | Customizable trainer card with actual party; original badge art for Gen 1–6 and Paldea, all 18 SV badge records, Alola passport stamps and compact Hisui rank stars; badges in one horizontally scrolling row; separate HGSS region bytes and SV receipt precedence corrected. LGPE capture totals and Z-A Royale totals. LGPE gym flags and exact Galar art remain open |
| Fashion | Searchable visual catalog with category/color illustrations, ownership switches and category/all unlock; named PLA/SWSH/BDSP/SV items, numeric Alola/ZA entries; XY/LGPE bulk unlock; advanced equipped IDs. Exact garment art and complete names for all titles remain open |
| Personal journals | Atomic whole-party capture to new/existing teams with independent file snapshots; add current/box/party Pokémon or journal-only species pages; names, favorites, stories, four card styles; teams of up to six with ordered membership, notes and emblems; custom cover colors and shared presets; searchable tabs and versioned JSON backups with Pokémon snapshots; reviewed game-format team generation/export and empty-slot placement; personal notes and styles never exported into game data |
| Treat cases | Hoenn Gen 3 Pokéblocks, Sinnoh Gen 4 Poffins, ORAS block counts, Gen 6 Puffs, Alola Beans, BDSP Poffins; individual edits and applicable bulk controls |
| Game Extras | 43 game-aware editors; Gen 3–5 adventure collections, mail, Battle Revolution wardrobe and BDSP unlocks, including Pokémon Link, Unova Global Link and downloaded content/C-Gear, Gen 4 battle videos, Battle Revolution passes and teams, Secret Bases (Gen 3/6), Pokéathlon and Festival Plaza/Battle Agency, Join Avenue, Pokégear contacts, BDSP Underground, Max/Tera Raids and seven-star history, plus Hall of Fame (Gen 1/3/6/7), Geonet, Unity Tower and the read-only XY berry viewer, plus Gen 4 Underground, Gen 4/5 Chatter and Gen 6 Super Training save records, stationary events, GS Ball, Gen 3 roamers/clocks, Apricorns, honey trees, medals/habitats, XY roamers, O-Powers, Alola collectibles, catch counts, stickers and donuts; searchable entries, explicit Back navigation and Undo |
| Save Tools | Species/base-stat guide; typed indexed event variables; named Switch blocks, typed scalar edits, exact-size block import/export and ZIP archive; same-family save comparison with report, reviewed folder/typed-ZIP bulk imports and all raw export layouts |
| Inventory | Searchable Add Items with stack top-up, owned counts, per-item limits, full-pouch checks and undo; pouches, quantities, item selection where appropriate, additional flags; engine consistency rules enforced |
| Pokédex | Basic seen/caught; Legends: Arceus 30 research counters, species milestones, reporting, Path of Solitude, per-form variant flags, displayed appearance and size records; BDSP and SV per-entry form/language/display details; Gen 4–7 forms/languages/display, ORAS DexNav counts, LGPE sizes, SWSH counts/Gigantamax and ZA per-form/Alpha/Mega records; game-aware bulk seen/caught/completion with shiny/language options; remaining form/language/appearance group Set All/Clear; real-save QA open |
| Advanced save | Navigable public nested structures and scalar properties, with persistence checks; not arbitrary arrays, buffers, or all Windows dialogs |
| Raids | Dedicated SWSH regional dens, SV regional crystals/daily seeds/copy actions and seven-star records; hexadecimal seeds; no encounter/reward simulator |
| Events | Searchable PKHeX labels/categories and known values, raw indices, typed edits, revision guards and Undo for Gen 2–7/LGPE/BDSP; BDSP system flags; Alola QR synchronization; read-only two-save/current-workspace comparison with copy/export report. Z-A adds all 15 keyed collections, custom name-file loading, compaction and key-based comparison; Gen 1 resets and Crystal GS Ball have dedicated tools |
| Batch | Entity filter/edit syntax, scoped to boxes or party, staged review/apply, undo, protection checks; folder snapshots with reviewed export of edited copies; box, slot and source-identifier metadata filters implemented |
| Showdown | Single-set import/export plus staged 1–6-set team generation, legality review, guarded placement into empty slots and individual-file export |
| Legality | Original reports/settings plus offline Auto-Legality Mod encounter generation, change/report preview, guarded apply and Undo; no guarantee that impossible sets can be legalized |
| File opening | Open or drop one local save/Pokémon file; unsaved-change prompts, extensionless paths, size/type validation; Pokémon-file drops into box slots implemented with conversion, presence/protection/revision checks and Undo; Finder gesture QA remains open |
| Recovery | 25-step undo/redo; originals kept untouched; failed-open draft/view preservation; atomic export; reparse/checksum verification of save output |
| Sprites | Regular and shiny artwork for all 1,025 species, including 142 regular/shiny Gen 9 pairs from user-supplied Ezerart sheets; automatic shiny selection in editor, slots and library; transparent padding trimmed at render time |
| Cosmetics, ribbons & markings | Format-aware Appearance/Awards/Contests/History sections; size scalars/classifications and measured size/CP recalculation, Shiny Leaves/crown, special traits, contest conditions and guided memory/care cards; dedicated ribbon/mark editor, upstream artwork and names, legality hints, count controls, equipped titles, suggestions, clear/all, shape colors and IV-based markings |
| Super Training | 30 regular/secret and six distribution medals, Gen 6 secret flags and bags/hits, Gen 7 retained records, undo |
| Move editor | Type icons/colors, search and learnability filters, selected-slot legality explanations; current/relearn suggestions with typed edits, alternate sets, clear no-change feedback and Undo |
| Move history | Move-shop purchases/mastery and TM/TR flags, search, compatibility filter, upstream suggestion actions, undo |
| Storage | Whole-save collection search, shiny/Alpha filters, box Pokémon-file export and CSV reports; multiple persistent folder roots with add/remove/reopen, bounded read-only scan and snapshot loading; advanced storage expressions and previous/next navigation; configurable CSV columns/order and filtered-row export |
| Encounters | Species/form/origin/type/four-move/shiny-rule search, read-only PKHeX expressions, readable results and preparation with optional current-editor criteria; configurable result limit (default 2,000); independent full criteria fields and local remembered/imported trainer profiles; database preferences remain open |
| Move-plus | Z-A upgrade flags, search/filter, natural/TM/Seed suggestions, clear, undo, PA9 file roundtrip |
| Mystery Gifts | Illustrated event-card gallery with search, selected-card actions and centered empty states; bundled event library, card-file export and gift Pokémon preparation/conversion; editable game albums with import/export, status, deletion, received-ID history and library installation; session folder libraries with duplicate detection and source protection; language/origin/generation/species/item/moves/shiny/egg/source filters; read-only batch expressions and atomic filtered-card export |
| Preferences | Legality, converter, Showdown import, slot write, save language, native appearance |
| Distribution | Local ad-hoc-signed app and source; Intel build with engine checks under Rosetta; no Developer ID notarization or physical Intel QA |

## Windows preference groups still requiring integration or replacement

Startup/autoload, backup manager, local resource discovery, privacy/display options, sprites/overlays, sounds, hover behavior, battle-template export preferences, drawing configuration, entity-editor behavior, Pokémon database, encounter database, Mystery Gift database, report configuration, slot-export preferences, and plugin loading are not fully reproduced. Only preferences with implemented behavior are shown as active native controls.

## Upstream dialog inventory

The table below inventories source dialogs, not independently verified user-visible features. “Partial” means an overlapping workflow exists in the native app; it does **not** mean the original dialog's full behavior has been ported. “Advanced fields only” means only scalar fields reachable through the generic browser may be available. This list is a roadmap, not a claim of parity.

| Windows source dialog | Native status |
|---|---|
| `BoxExporter.cs` | Implemented — current/all boxes, separate folders, slot prefixes, optional empty slots; persistent folder/naming/index preferences implemented |
| `EntitySearchSetup.cs` | Partial — advanced read-only property expressions, storage filtering and previous/next navigation; guided rule builder now available for Storage, encounters and gifts; freeform expressions remain available |
| `KChart.cs` | Implemented — searchable species/forms, game personal table, types, abilities, base stats and native filter |
| `Misc/EntitySummaryImage.cs` | Implemented — native summary PNG/clipboard export, optional trainer details and light/night colors |
| `Misc/PropertyComparer.cs` | Implementation helper, not a user-facing dialog |
| `Misc/SortableBindingList.cs` | Implementation helper, not a user-facing dialog |
| `PKM Editors/BatchEditor.cs` | Partial — boxes/party, reviewed batch apply |
| `PKM Editors/MemoryAmie.cs` | Implemented — guided OT/HT memory narratives, context-sensitive arguments and feelings, friendship/affection/care, five dependent residence pairs, clear controls, staged preview, identity/revision guards and Undo; origin-region fields remain in History |
| `PKM Editors/MoveShopEditor.cs` | Implemented — individual purchases/mastery, compatible filtering, current/all/clear actions |
| `PKM Editors/PlusRecordEditor.cs` | Implemented — Z-A move-plus flags, named/type rows, learnable filtering, natural/TM/Seed suggestions, clear and undo |
| `PKM Editors/RibbonEditor.cs` | Implemented — named/icon rows, count limits, equipped picker, hints and original bulk actions; native layout differs and visual QA remains open |
| `PKM Editors/SuperTrainingEditor.cs` | Implemented — all 36 regimens, Gen 6 secret flags/bags/hits, transferred Gen 7 medals, all/clear and undo; visual QA remains open |
| `PKM Editors/TechRecordEditor.cs` | Implemented — individual flags, search/filter, current/legal-all/force-all/clear actions; per-evolution coloring differs |
| `PKM Editors/Text.cs` | Implemented for Pokémon nickname/OT/HT: raw bytes, encoding/decoding, special characters, species/language/generation layers, clear hidden bytes, preview, stale guards and Undo. Hall of Fame Gen 1/3/6 nickname and Gen 6 trainer bytes are integrated; trainer and applicable rival names plus Gen 3 Secret Base trainer bytes implemented |
| `ReportGrid.cs` | Partial — whole-save searchable table and CSV report; configurable CSV columns/order (including exposed PKM properties) and filtered-row export; native table columns remain fixed |
| `SAV_Database.cs` | Partial — current-save search; folder scan/search/filters and snapshot loading implemented; multiple remembered folder roots with add/remove/reopen and overlapping-root deduplication; reviewed folder batch copy-export and metadata filters implemented |
| `SAV_Encounters.cs` | Partial — species/origin/type/shiny-rule/move search, normal forms, preview and preparation; four move filters, explicit form filter, read-only batch expressions and optional editor criteria; independent criteria, remembered trainer profiles and persistent result limit implemented |
| `SAV_FolderList.cs` | Implemented — multiple folders, add/remove, remembered roots, manual reopen, bounded recursive scans and overlap deduplication |
| `SAV_MysteryGiftDB.cs` | Partial — bundled and session folder libraries; language, origin, generation/range, kind, species, item, moves, shiny, egg and source filters; card export and Pokémon preparation. read-only Windows batch-expression search and atomic filtered-card folder export added; database settings remain open |
| `Save Editors/Gen1/SAV_EventReset1.cs` | Implemented — named stationary encounter resets and Undo |
| `Save Editors/Gen1/SAV_HallOfFame1.cs` | Partial — teams, species/level/nickname, clear count, party registration, delete/clear, Undo; staged raw name bytes, encoding/layers/clear, revision guards and Undo implemented |
| `Save Editors/Gen2/SAV_Misc2.cs` | Implemented — Crystal GS Ball event enable; hidden for Gold/Silver |
| `Save Editors/Gen3/PokeBlock3CaseEditor.cs` | Implemented — individual values, fill/empty and Undo |
| `Save Editors/Gen3/SAV_HallOfFame3.cs` | Partial — all 50 teams, species/level/nickname/IDs/PID, shiny previews, party import/current-or-all, clear and Undo; staged raw name bytes, encoding/layers/clear, revision guards and Undo implemented |
| `Save Editors/Gen3/SAV_Misc3.cs` | Implemented — coins, rival/card species, Joyful records, Frontier symbols/records/pass, island flags/tickets, Mirage Island, decorations, paintings and adventure records; synthetic R/S/E/FR/LG persistence verified |
| `Save Editors/Gen3/SAV_RTC3.cs` | Implemented — initial/elapsed clocks, reset and berry fix |
| `Save Editors/Gen3/SAV_Roamer3.cs` | Implemented — species, PID, health, level, active state and IVs; original glitch explained |
| `Save Editors/Gen3/SAV_SecretBase3.cs` | Implemented — registered base profiles, trainer IDs, all six battle members and 16 decoration positions, Undo and serialized readback |
| `Save Editors/Gen4/PoffinCase4Editor.cs` | Implemented — individual values, fill/empty and Undo |
| `Save Editors/Gen4/PokeGear4Editor.cs` | Implemented — named ordered contacts, Add All, non-trainers and Clear All; both trainer genders tested |
| `Save Editors/Gen4/Pokeathlon/PokeathlonConnection4Editor.cs` | Implemented — typed nested Pokéathlon fields, including linked/solo event records, participants and trainers; Undo and save roundtrips |
| `Save Editors/Gen4/Pokeathlon/PokeathlonEventData4Editor.cs` | Implemented — typed nested Pokéathlon fields, including linked/solo event records, participants and trainers; Undo and save roundtrips |
| `Save Editors/Gen4/Pokeathlon/PokeathlonEventRecord4Editor.cs` | Implemented — typed nested Pokéathlon fields, including linked/solo event records, participants and trainers; Undo and save roundtrips |
| `Save Editors/Gen4/Pokeathlon/PokeathlonEventTrainer4Editor.cs` | Implemented — typed nested Pokéathlon fields, including linked/solo event records, participants and trainers; Undo and save roundtrips |
| `Save Editors/Gen4/Pokeathlon/PokeathlonParticipant4Editor.cs` | Implemented — typed nested Pokéathlon fields, including linked/solo event records, participants and trainers; Undo and save roundtrips |
| `Save Editors/Gen4/Pokeathlon/PokeathlonSpeciesForm4Editor.cs` | Implemented — typed nested Pokéathlon fields, including linked/solo event records, participants and trainers; Undo and save roundtrips |
| `Save Editors/Gen4/SAV_Apricorn.cs` | Implemented — named quantities, item icons, Give All and Clear |
| `Save Editors/Gen4/SAV_BattlePass.cs` | Implemented — all 187 passes, profiles/appearance/messages/records, unlock/swap/delete, pass files and stored team View/Set/Remove/import/export; English/Japanese synthetic serialization verified; full UI/game-load QA remains open |
| `Save Editors/Gen4/SAV_DLC4.cs` | Implemented — initialized Pt/HGSS battle-video slots, encrypted/decrypted file transfer, team summaries and destination block metadata preservation; uninitialized slots remain unavailable as upstream |
| `Save Editors/Gen4/SAV_Gear.cs` | Implemented — Battle Revolution clothing for six models, shared badges, special shiny outfits, Give All and reset; this upstream dialog is unrelated to Pokégear |
| `Save Editors/Gen4/SAV_Geonet4.cs` | Implemented — named countries/regions, marker states, global flag, all/legal/clear, home preservation and Undo |
| `Save Editors/Gen4/SAV_HoneyTree.cs` | Implemented — named trees, timer/group/slot/shake controls, species preview, Munchlax locations and Make Catchable |
| `Save Editors/Gen4/SAV_Misc4.cs` | Implemented — Frontier records/prints, Pokétch apps/Dot Artist canvas, Pokéwalker, Fly, seals, accessories, backdrops and records; initialized Hall lifetime block supported; real-save/game-load QA open |
| `Save Editors/Gen4/SAV_Pokeathlon4.cs` | Implemented — points, Data Cards, daily shop flags, lifetime/course/event records, participants, connected trainers and species medals; synthetic HG/SS save roundtrips |
| `Save Editors/Gen4/SAV_Pokedex4.cs` | Partial — upgrade mode, seen/caught, gender order, supported languages/form order and Spinda PID; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen4/SAV_Trainer4BR.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen4/SAV_Underground.cs` | Implemented — all four 40-slot pouches, sphere sizes, 13 scores, compaction, pouch actions and Undo; synthetic D/P/Pt serialized readback |
| `Save Editors/Gen5/CGearImage.cs` | Implemented — native PNG preview/import/export, exact dimensions, color/tile validation and BW/B2W2 binary layout conversion; automatic color reduction is not performed |
| `Save Editors/Gen5/Join Avenue/IJoinAvenueSpecificEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/JoinAvenueAssistantSpecificEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/JoinAvenueEntityGeneralEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/JoinAvenueFanSpecificEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/JoinAvenueListEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/JoinAvenueSettingsEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/JoinAvenueVisitorSpecificEditor.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/Join Avenue/SAV_JoinAvenue.cs` | Implemented — grouped profiles/settings, shops, dates, records/trivia/activities, visit history and same/cross-kind person-file import/export; synthetic B2/W2 roundtrips tested |
| `Save Editors/Gen5/SAV_DLC5.cs` | Implemented — C-Gear/Pokédex skins, musicals, Memory Link, battle videos, PWT and Pokéstar files with game-aware slots; Battle Test remains an advanced file transfer with upstream in-game support unresolved |
| `Save Editors/Gen5/SAV_GlobalLink5.cs` | Implemented — local date/upload/status fields, item rewards, furniture/name records and synchronization flags; no retired online service connection |
| `Save Editors/Gen5/SAV_Medals5.cs` | Partial — named medals, states/dates/unread, rank, pinned medal, file import/export and habitat flags; multi-selection status/date/read/clear controls implemented with atomic Undo |
| `Save Editors/Gen5/SAV_Misc5.cs` | Implemented — Fly, BW roamers/Liberty Ticket and resident files, B2W2 keys, Entralink, Funfest, all 530 forest slots/random fill, Subway, Musical and records; numeric forest forms and stored phrase labels remain less guided than Windows |
| `Save Editors/Gen5/SAV_Pokedex5.cs` | Partial — national modes, all seen/display/form regions, supported species languages and Spinda PID; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen5/SAV_UnityTower.cs` | Implemented — named countries/regions, all country floors, global/tower flags, all/legal/clear and Undo |
| `Save Editors/Gen6/SAV_BerryFieldXY.cs` | Implemented — all 32 plots, eight stored values each; read-only as in Windows |
| `Save Editors/Gen6/SAV_BoxLayout.cs` | Partial — native names, wallpaper previews and whole-box move/swap; unlock count and raw box flags implemented; wallpaper-grid UI verified using generated Brilliant Diamond data |
| `Save Editors/Gen6/SAV_HallOfFame.cs` | Partial — 16 teams, clear/date flags, all member fields, named forms/moves/items, delete compaction and Undo; stored-name bytes and clipboard summaries implemented |
| `Save Editors/Gen6/SAV_Link6.cs` | Implemented — source/enabled state, item rewards, BP/Miles, Pokémon reward summaries and `.pl6` files; templates are preserved in the file, matching the Windows summary scope |
| `Save Editors/Gen6/SAV_OPower.cs` | Implemented — unlock flags, field/battle levels, points, Give All and Clear |
| `Save Editors/Gen6/SAV_PokeBlockORAS.cs` | Implemented — 12 quantities, fill/empty and Undo |
| `Save Editors/Gen6/SAV_PokedexORAS.cs` | Partial — national modes, forms/appearance/language, Spinda PID and DexNav encounter/obtained counters; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen6/SAV_PokedexXY.cs` | Partial — national modes, foreign flags, forms/appearance/language and Spinda PID; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen6/SAV_Pokepuff.cs` | Implemented — individual type, varied/best fill, sort, empty and Undo |
| `Save Editors/Gen6/SAV_Roamer6.cs` | Implemented — species, encounter count and roaming state |
| `Save Editors/Gen6/SAV_SecretBase.cs` | Implemented — own/30 visitor bases, profiles/messages/ranks, battle teams, placements, decoration stock and Give All, .sb6 files, deletion and Undo |
| `Save Editors/Gen6/SAV_SuperTrain.cs` | Implemented — 32 stages, both record holders and fractional times, 12 training bags, stage/distribution flags, clearing and Undo; synthetic XY/ORAS serialized readback |
| `Save Editors/Gen6/SAV_Trainer.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen7/SAV_Capture7GG.cs` | Implemented — per-species counts, totals, recalculation and Pokédex-aware bulk counts |
| `Save Editors/Gen7/SAV_FestivalPlaza.cs` | Implemented — plaza/facilities/owners/messages, coins/rank/date, phrases/rewards; USUM Battle Agency grade/records/trainers, sunglasses, Pokémon files/party copying and Undo |
| `Save Editors/Gen7/SAV_HallOfFame7.cs` | Implemented — first/current teams and USUM starter encryption constant, Undo |
| `Save Editors/Gen7/SAV_Pokebean.cs` | Implemented — named counts, fill/empty and Undo |
| `Save Editors/Gen7/SAV_PokedexGG.cs` | Partial — caught, seen/display forms and languages, all four size-record categories; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen7/SAV_PokedexSM.cs` | Partial — caught, seen/display form regions and languages; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen7/SAV_Trainer7.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen7/SAV_Trainer7GG.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen7/SAV_ZygardeCell.cs` | Implemented — named cell/sticker states and totals; Collect All Remaining and USUM linked record |
| `Save Editors/Gen8/PokedexResearchTask8aPanel.cs` | Implemented — all listed tasks, bonus points, reported/unreported/inconsistent milestones, game-derived read-only tasks and reporting |
| `Save Editors/Gen8/SAV_BlockDump8.cs` | Named block browser, typed edits, exact-size binary import/export, typed ZIP archive, reviewed folder/archive bulk import, same-family save comparison/report and all raw export options; real-game validation remains open |
| `Save Editors/Gen8/SAV_FlagWork8b.cs` | Implemented — named flag/system/work categories, known values, raw indices, typed changes, Undo and file/workspace comparison with reports; real-save progression QA open |
| `Save Editors/Gen8/SAV_Misc8b.cs` | Implemented — Core legendary-event readiness/actions, zone unlocks, trainer battle flags and fashion unlocks; real-game event progression QA open |
| `Save Editors/Gen8/SAV_Poffin8b.cs` | Partial — individual type/level/flavors/smoothness, fill/empty and Undo; dedicated cooking-count control implemented |
| `Save Editors/Gen8/SAV_PokedexBDSP.cs` | Partial — native per-entry state, gender/shiny, languages, forms and dex unlocks; bulk seen/caught/completion implemented; real-save QA remains open |
| `Save Editors/Gen8/SAV_PokedexLA.cs` | Partial — research/reporting/Path of Solitude plus per-form variant flags, display appearance and size records; full task definitions implemented; real-save feedback is user-owned |
| `Save Editors/Gen8/SAV_PokedexResearchEditorLA.cs` | Implemented — all 30 counters, task labels, milestones, validation, undo; exported real-save QA remains open |
| `Save Editors/Gen8/SAV_PokedexSWSH.cs` | Partial — caught/Gigantamax, battle count, display settings, forms/genders/shinies and languages; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen8/SAV_Raid8.cs` | Implemented — all three regions, hashes/seeds, difficulty, encounter roll, den flags and derived switches; SW/SH serialized readback |
| `Save Editors/Gen8/SAV_SealStickers8b.cs` | Implemented — named available/lifetime counts, obtained flags, Give All and Clear; sticker artwork remains open |
| `Save Editors/Gen8/SAV_Trainer8.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen8/SAV_Trainer8a.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen8/SAV_Trainer8b.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen8/SAV_Underground8b.cs` | Implemented — named items/counts/new/favorite, Give All and Empty All; BD/SP serialized readback |
| `Save Editors/Gen9/DonutEditor9a.cs` | Implemented — recipes, berries, powers, date, stars, calories and level boost; calculate from berries |
| `Save Editors/Gen9/DonutFlavorProfile9a.cs` | Implemented — native five-flavor bar chart with exact values |
| `Save Editors/Gen9/EventWorkGrid64.cs` | Implemented — all 64/128/192-bit key layouts and tuple values, full unsigned values, flags, search/paging, custom key names, clear/compaction and Undo |
| `Save Editors/Gen9/SAV_Donut9a.cs` | Partial — searchable slots, artwork, individual files, clear, clone-to-all and preset collections; hex clipboard import/export and staged file drops implemented |
| `Save Editors/Gen9/SAV_DonutGenerator9a.cs` | Partial — random level-3 and shiny presets plus explicit slot range/flavor-pool generation matching the Windows generator; Undo and range guards |
| `Save Editors/Gen9/SAV_Fashion9.cs` | Partial — named individual ownership, category/all unlock, advanced equipped IDs; exact clothing thumbnails remain open |
| `Save Editors/Gen9/SAV_FlagWork9a.cs` | Implemented — all 15 collections, custom key-name text files and key-based save comparison including added/removed entries; synthetic serialization and focused native checks pass; real-game QA remains open |
| `Save Editors/Gen9/SAV_Pokedex9a.cs` | Partial — per-form seen/caught/shiny, genders/languages, Alpha/Mega flags and display settings; bulk seen/caught/completion implemented; form/language/appearance group Set All/Clear implemented; real-save QA remains open |
| `Save Editors/Gen9/SAV_PokedexSV.cs` | Partial — original-format language/form/gender/shiny/new/display records; bulk seen/caught/completion implemented; real-save QA remains open |
| `Save Editors/Gen9/SAV_PokedexSVKitakami.cs` | Partial — active updated form/language/model/gender flags and regional display records; bulk seen/caught/completion implemented; real-save QA remains open |
| `Save Editors/Gen9/SAV_Raid9.cs` | Implemented — all three regions, daily/individual seeds, crystal fields and copy with/without seed; SL/VL serialized readback |
| `Save Editors/Gen9/SAV_RaidSevenStar9.cs` | Implemented — identifiers and captured/defeated history, serialized readback |
| `Save Editors/Gen9/SAV_Trainer9.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Gen9/SAV_Trainer9a.cs` | Partial — common trainer fields and exposed structures |
| `Save Editors/Misc/SAV_Accessor.cs` | Native Advanced Save equivalent: nested public models and model collections, persisted-change validation. Raw buffers remain in Save Tools |
| `Save Editors/SAV_BoxList.cs` | Partial — staged sorting/modification/deletion plus whole-box move/swap with names and wallpapers; protected boxes reject moves |
| `Save Editors/SAV_BoxViewer.cs` | Partial — whole-save collection table and slot navigation |
| `Save Editors/SAV_Chatter.cs` | Implemented — recorded voice enable/clear, calculated confusion chance, PCM import/export, WAV export and native playback/waveform; synthetic Gen 4/5 readback; audible output still needs manual QA |
| `Save Editors/SAV_EventFlags.cs` | Implemented — researched labels/categories, known values and raw indices, typed edits/Undo, two-save or current-workspace comparison and copy/export reports; external comparison-file drag gestures remain open |
| `Save Editors/SAV_EventFlags2.cs` | Implemented — researched labels/categories, known values and raw indices, typed edits/Undo, two-save or current-workspace comparison and copy/export reports; external comparison-file drag gestures remain open |
| `Save Editors/SAV_EventWork.cs` | Implemented — researched labels/categories, known values and raw indices, typed edits/Undo, two-save or current-workspace comparison and copy/export reports; external comparison-file drag gestures remain open |
| `Save Editors/SAV_GroupViewer.cs` | Implemented — Stadium registered-team browser with Pokémon View |
| `Save Editors/SAV_Inventory.cs` | Partial — native inventory editor |
| `Save Editors/SAV_MailBox.cs` | Implemented — Gen 2–5/Stadium 2 mailbox, stationery, author/text/phrase codes, portraits, party mail links, clear with optional item detachment, supported reorder, Gen 2 duplicate storage and Undo; raw text/trash tool remains separate |
| `Save Editors/SAV_SimplePokedex.cs` | Implemented — basic seen/caught controls and bulk actions where supported; Gen 4/5/7 support detection corrected |
| `Save Editors/SAV_SimpleTrainer.cs` | Advanced fields only; dedicated dialog not ported |
| `Save Editors/SAV_Wondercard.cs` | Partial — native album import/export, library installation, used flags where persisted, delete/pack, received-ID history, Gen 4 special slots and Gen 5 encryption; QR card import/export implemented; staged gift-card drops implemented; real-save testing is user-owned |
| `Save Editors/TrainerStat.cs` | Implemented — named, bounded trainer-record editor with Undo |
| `SaveHandlerTroubleshooter.cs` | Implemented — native format, handler, edition and language selection; failed-open workspace preservation |
| `SettingsEditor.cs` | Partial — active core preferences, game themes and appearance; other Windows preferences remain open |

Upstream non-designer dialog/source entries inventoried: 129.

## Validation limits for 0.5

540 automated protocol operations passed. The new album has a generated Black 2 save export/reopen test with valid checksums; Gen 4/6/7 checks use sample memory or individual card files. Arceus details are tested in sample memory, not exported personal saves. New research navigation was inspected in a separate QA app, but the UI automation connection failed before the detail sheet and album could be visually verified. Full Windows parity remains open.

## Changes and validation limits for 0.6

The dedicated ribbon/marking and Super Training editors are implemented and tested through the bundled engine, including Pokémon-file export/reopen across supported fixture formats. File-opening/import now passes the filename format hint to PKHeX to resolve ambiguous data. The regression tests explicitly assert the returned Pokémon format. Native compilation passes, but the computer-use connection is still unavailable, so the new layouts require hands-on UI QA. Full Windows parity remains open.

## Changes and validation limits for 0.7

Box-slot moves/swaps, names, wallpaper choices and whole-box reordering use PKHeX.Core with atomic rollback and undo. Seven suites passed 1,160 protocol operations, including 118 new box operations. Generated Black 2 and Brilliant Diamond exports retain valid checksums; a locked battle-team fixture rejects moves. Native sample UI checks cover names, reordering, Move / Swap, and Undo. Drag gestures and the wallpaper grid remain unverified. Arceus and Z-A intentionally expose fixed backdrops. Box unlock flags, file-drop workflows, party moves and full Windows parity remain open.

## Changes and validation for 0.7.1

Fixed a reproduced first-presentation bug in the shared choice sheet: Battle Version showed zero options despite receiving 83. The options and their presentation now use one immutable payload. Compact dropdowns use the same approach. Choice results expose accessible buttons, counts, no-match messages and single-result Return selection. Form/location labels refresh on species/origin-game changes. Native QA verified the formerly empty sheet, species filtering, selecting Eevee, and Undo. Wallpaper previews, selecting Desert, and Undo passed on generated Brilliant Diamond data. Drag gesture validation and full Windows parity remain open.

## Changes and validation for 0.8

More now presents format-specific guided sections with readable labels and descriptions, rather than the full raw field list. Main/Stats/Moves/Ribbons shortcuts are in a collapsed guide. The technical sheet preserves all fields, original IDs, edit permissions and name/ID search. Common technical values have explanations, including the distinction between Alpha status and the encounter move. Five-format catalog checks retained 371/316/355/330/356 original fields for Arceus/X/Scarlet/Ultra Sun/Brilliant Diamond, while the everyday page contains 1/10/9/10/13 guided controls respectively. The Arceus native overview, Favorite toggle and Undo, search handoff to HOME tracker, and original-ID lookup for AlphaMove passed UI checks. Eight protocol suites passed 1,210 operations, including valid Tera sentinel/Stellar values, invalid-type rejection and Pokémon-file roundtrips. Full Windows parity remains open.

## Changes and validation for 0.10

Encounter search/preparation, file-folder browsing, Z-A move-plus records, and box unlock/flag controls are added. Windows advanced encounter criteria, multi-folder management, specialized save dialogs and full parity remain open. The regular/shiny sprite sheets are supplied artwork attributed to Ezerart; the cell manifests retain all 284 source regions and the Shiny flag selects the corresponding icon. See VALIDATION.md for automated and native QA coverage.

## Changes and limits for 0.11

Current and relearn move suggestions now use the upstream core, with per-slot legality explanations. Right-click View, visible appearance controls, custom colors, 13 palettes, and loaded-save game identity are added. BDSP and SV have native per-entry Pokédex details. SV switches adapters according to the active block; no vendor code was changed. Broad Pokédex actions, the remaining specialized Windows dialogs, full localization and full Windows feature parity remain unfinished. Fourteen game logos are included; other formats have a correctly named badge.

## Changes and limits for 0.12

All 1,025 species now request shiny resources when Shiny is enabled. Form fallback preserves shiny status, and the supplied Gen 9 sheets remain intact. The move picker uses PKHeX.Core learnability with type icons and colors; full selected-slot legality remains separate. Suggestions include typed drafts and attempt another valid set when the default already matches. Fixed or restricted encounters can have no alternative, which is now explained explicitly. Suggestions are not competitive optimization or a full-Pokémon legality generator. The remaining Windows dialogs and distribution limitations listed above are still open.

## Changes and limits for 0.13

KeepSake adds a customizable trainer card, item and ball artwork, nickname-first names, gender colors, improved trait emblems, configurable slot/drag animations and rich hover previews. Stats now contains Tera controls, characteristic text and IV/EV randomization. The Pokédex grid uses dedicated basic-flag adapters for SV/BDSP/SWSH/PLA/ZA instead of treating their absent generic setters as lack of a Pokédex. Give All actions are explicit and undoable. Fashion actions use upstream unlock logic; advanced clothing IDs are not presented as a named clothing catalog. Sidebar entries follow the loaded save's actual capabilities. Full Windows parity and notarized distribution remain open.

## 0.15 — release-candidate preparation

Adds local journals/favorites/display names/card styles, visual wardrobe controls, six treat-case adapters and shared save tools. See the current implementation table for scope. Tests cover each generation with synthetic workspaces and selected serialized fixtures; all-title real-save validation is still open. Generic fashion illustrations are deliberately distinguished from exact game art. The earlier 0.13 fashion summary describes that historical release, before named catalogs and PLA unlocking.

### 0.31 appearance refinement

Pokérus status shortcuts now share the Shiny/Egg/Alpha row. Pixel sprite selection also governs appearance previews and theme cards. A distinct Game portraits option adds Scarlet/Violet and Arceus artwork with exact-variant HOME/pixel fallback. These appearance changes do not close any remaining specialized editor or distribution gate.

## 0.32 focused parity update

Adds explanatory Pokérus hover help; multi-folder library management; storage property filters and result navigation; custom CSV reports; gift expressions and filtered bulk export; encounter form/four-move/property filters and current-editor criteria; staged six-set Showdown team import; Hall of Fame stored-name bytes; Pokédex boolean-group shortcuts; custom Z-A donut range/flavor generation; guarded file-to-box-slot import. The existing core remains unmodified.

Full parity is still open: specialized trainer workflows, independent encounter criteria/trainer database, folder batch operations, remaining settings/backup/autoload/localization/QR/plugin workflows, party dragging and remaining text-editor integrations. Cross-window Finder gestures and all-title real-save/in-game validation are separate open checks. Signing/notarization and hosting have not begun.

## 0.33 focused parity update

Independent encounter criteria expose the upstream nature, gender, ability permissions, shiny rule, six IVs, level range, Hidden Power, random form and mutation flags. Copy from Editor and Reset do not edit the loaded Pokémon. Remembered trainer profiles preserve origin, IDs, gender, language and 3DS regional fields, can be imported from save/Pokémon files, and are checked against the selected encounter origin. Fixed event trainers remain controlled by the core.

Folder batches reuse the Windows entity batch engine over selected visible library snapshots. Preview lists changes/errors; export writes a new directory atomically and checks source hashes before writing. Originals and the loaded save are not modified. This is the native copy-export workflow, not Windows in-place folder overwrite parity.

Pokémon QR export uses the upstream message generator, including PK7 binary payloads. Native QR decoding handles byte, numeric, alphanumeric and Latin-1/UTF-8 ECI segments without losing Gen 7 bytes. Image import prepares a Pokémon with conversion/checksum/presence checks and Undo. Mystery Gift QR and direct console-device testing remain open.

Party moves reject gaps, stale drags, protected box links and removal of the last non-Egg member. Empty-party-slot reorder compacts then appends. Gen 1/2 snapshots preserve packed and unpacked buffers to avoid normalization during rejected actions and Undo. Native Finder and gesture verification remains a separate gate.

Intel engine and SwiftUI binaries build successfully. Engine protocol tests pass under Rosetta on Apple Silicon; this does not substitute for physical Intel hardware testing.


### Current completion work (unarchived)

- Files & Startup preferences: optional last-document reopening, original-save backups with content hashes, integrity-checked export to a new file, and bounded recursive save-folder discovery.
- Game-data localization (ten bundled languages) and localized Showdown/community-format exports. The app interface is still English.
- Trainer Adventure Details: applicable coordinates, dates, regional metadata, language, appearance, currencies and DLC structures; date/time edits retain time and UTC semantics. Z-A trainer gender changes reset fashion as upstream does.
- Recorded Scarlet/Violet and Z-A trainer photos can be viewed and exported as PNG.
- Mystery Gift QR import/export through the local library, explicit format selection for ambiguous card sizes, duplicate detection and size limits.
- All Hisui research task definitions, including derived quest/form tasks, bonus points and differentiated milestone status.
- Pokémon summary image export/copy and guided search-rule creation.

Real-save testing is user-owned and is not a prerequisite for continuing implementation. Full Windows parity is not claimed. Remaining offline work includes specialized trainer/text workflows, nested structures and database behaviors. By explicit release-scope decision, other Windows plugins and LiveHeX are excluded, and native interface translation is deferred. Auto-Legality and ten-language game-data catalogs remain included. No interim release ZIP is produced for this work.

### Trainer and preferences completion

Trainer name-byte preview/apply supports the save’s own string codec with stale-edit guards and Undo. Named trainer records use upstream record labels and bounds. Trainer controls now expose applicable status/configuration and DLC fields, synchronize Sword/Shield league numbers and lifetime Watts, and reset Gen 6 overworld models on gender changes. Box-export naming preferences, encounter result limits and CSV column presets persist. BDSP exposes its Poffin cooking count. These additions do not claim completion of every specialized trainer action.

Mystery Gift album file drops now stage the file before slot import, use the existing format/compatibility validation and Undo, and reject stale workspace revisions. Native compilation verifies the drop adapter; physical Finder gesture testing remains user-owned.

## RC1 closeout

The current batch adds named trainer progress and unlocks, Sword/Shield display teams, Alola destination/Battle Tree/throw-style/Surf controls, Battle Maison, Stadium registered teams, compatible edition switching, rotation/date controls, source-aware batches, medal multi-selection, donut clipboard/drop, Hall of Fame summaries, format-directed opening, rival/Secret Base name bytes and a configurable report preview. 842 focused protocol requests passed before final packaging.

Native presentation differs from Windows: folder batches export new copies, arbitrary binary buffers use Save Tools, and the compact library table has fixed summary columns while the report view offers custom columns. Some garment/sticker artwork remains illustrative or absent. Exact Windows-only display/preferences equivalence is not asserted. The archived partial entries above are an inventory of native coverage, not seven unimplemented feature categories. Signing/notarization and hands-on release feedback are still outstanding.
