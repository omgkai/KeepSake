using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

// This bridge keeps all binary parsing and writing in the unmodified PKHeX engine.
// The protocol is local stdin/stdout only; no network listener is created.
if(args.FirstOrDefault()=="--legalize-worker") {LegalityWorker.Run();return;}
var session = new EditorSession(args.FirstOrDefault());
string? line;
while ((line = Console.ReadLine()) != null)
{
    try
    {
        using var document = JsonDocument.Parse(line);
        var result = session.Handle(document.RootElement);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = true, data = result }));
    }
    catch (Exception ex)
    {
        while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
        if (Environment.GetEnvironmentVariable("PKHEX_TRACE") == "1") Console.Error.WriteLine(ex);
        Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
    }
}

sealed class EditorSettings
{
    public BoxExportSettings BoxExport { get; set; } = new() { FolderCreation=BoxExportFolderMode.FolderEachBox, FolderPrefix=BoxExportFolderNaming.IndexBoxName, FileIndexPrefix=BoxExportIndexPrefix.InBoxAndSlot };
    public int EncounterResultLimit { get; set; } = 2000;
    public bool BackupOnOpen { get; set; } = true;
    public string CatalogLanguage { get; set; } = "en";
    public string ExportLanguage { get; set; } = "en";
    public bool ExportCommunityFormat { get; set; }
    public LegalitySettings Legality { get; set; } = new();
    public EntityConverterSettings Converter { get; set; } = new();
    public SlotWriteSettings SlotWrite { get; set; } = new();
    public SaveLanguageSettings SaveLanguage { get; set; } = new();
    public SetImportSettings Import { get; set; } = new();
    public void Apply()
    {
        ParseSettings.Initialize(Legality);
        SaveLanguage.Apply();
        CommonEdits.ShowdownSetIVMarkings = Import.ApplyMarkings;
        CommonEdits.ShowdownSetBehaviorNature = Import.ApplyStatAlignment;
        SaveFile.SetUpdateDex = SlotWrite.SetUpdateDex ? EntityImportOption.Enable : EntityImportOption.Disable;
        SaveFile.SetUpdatePKM = SlotWrite.SetUpdatePKM ? EntityImportOption.Enable : EntityImportOption.Disable;
        SaveFile.SetUpdateRecords = SlotWrite.SetUpdateRecords ? EntityImportOption.Enable : EntityImportOption.Disable;
        EntityConverter.AllowIncompatibleConversion = Converter.AllowIncompatibleConversion;
        EntityConverter.RejuvenateHOME = Converter.AllowGuessRejuvenateHOME;
        EntityConverter.VirtualConsoleSourceGen1 = Converter.VirtualConsoleSourceGen1;
        EntityConverter.VirtualConsoleSourceGen2 = Converter.VirtualConsoleSourceGen2;
        EntityConverter.RetainMetDateTransfer45 = Converter.RetainMetDateTransfer45;
    }
}

record Choice(string value, string label);
record Field(string id, string label, string group, string value, string kind, bool editable, string help, string? lookup, Choice[] choices);
record Snapshot(SaveFile? Save, PKM? Entity, bool Dirty, bool Pending, int Box, int Slot, bool Party, string? EntitySourcePath);

sealed partial class EditorSession
{
    SaveFile? save;
    PKM? entity;
    string? sourcePath;
    string? entitySourcePath;
    int box, slot = -1;
    bool party, dirty, pending, demo;
    GameVersion sampleVersion;
    readonly Stack<Snapshot> undo = new(), redo = new();
    SaveFile? batchCandidate;
    string? batchToken;
    int revision, batchRevision;
    readonly string? settingsPath;
    EditorSettings settings = new();
    GameStrings strings = GameInfo.GetStrings("en");
    static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
    static readonly HashSet<string> hiddenEntity = new(StringComparer.Ordinal) { "Valid", "Checksum", "Sanity", "CurrentFriendship", "Status_Condition" };
    static readonly HashSet<string> unsafeSave = new(StringComparer.Ordinal) { "Version", "CurrentBox", "ID32", "TID16", "SID16", "Language", "PartyCount", "DaycareSeed", "DaycareEXP", "SecondsToStart", "SecondsToFame" };
    static readonly HashSet<string> trainerFields = new(StringComparer.Ordinal) { "OT", "Gender", "DisplayTID", "DisplaySID", "Money", "Coins", "BP", "PlayedHours", "PlayedMinutes", "PlayedSeconds" };

    public EditorSession(string? path)
    {
        settingsPath = path;
        if (path != null && File.Exists(path))
        {
            try { settings = JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(path)) ?? new(); }
            catch { /* A damaged preference file must not prevent opening the app. */ }
        }
        settings.Apply();
        strings = GameInfo.GetStrings(settings.CatalogLanguage); GameInfo.Strings = strings; GameInfo.CurrentLanguage = settings.CatalogLanguage;
    }

    static string S(JsonElement r, string name, string fallback = "") => r.TryGetProperty(name, out var v) ? v.GetString() ?? fallback : fallback;
    static int N(JsonElement r, string name, int fallback = 0) => r.TryGetProperty(name, out var v) ? v.GetInt32() : fallback;
    static bool B(JsonElement r, string name) => r.TryGetProperty(name, out var v) && v.GetBoolean();
    SaveFile RequireSave() => save ?? throw new InvalidOperationException("Open a save file first.");
    PKM RequireEntity() => entity ?? throw new InvalidOperationException("Select a Pokémon or open a Pokémon file first.");
    Snapshot Capture() => new(CloneForUndo(), entity?.Clone(), dirty, pending, box, slot, party, entitySourcePath);
    void Restore(Snapshot s)
    {
        save = s.Save; entity = s.Entity; dirty = s.Dirty; pending = s.Pending; box = s.Box; slot = s.Slot; party = s.Party; entitySourcePath = s.EntitySourcePath;
        if (save != null) ParseSettings.InitFromSaveFileData(save);
    }
    void Mutate(Action change)
    {
        var before = Capture();
        try { change(); }
        catch { Restore(before); throw; }
        undo.Push(before); redo.Clear(); revision++; batchCandidate = null;
        if (undo.Count > 25)
        {
            var keep = undo.Take(25).Reverse().ToArray(); undo.Clear(); foreach (var s in keep) undo.Push(s);
        }
    }

    public object Handle(JsonElement r)
    {
        switch (S(r, "op"))
        {
            case "legalityPreview": return GenerationPreview(r,false);
            case "teamPreview": return GenerationPreview(r,true);
            case "legalityApply": Mutate(()=>ApplyLegality(r)); return State();
            case "teamPlace": Mutate(()=>PlaceGeneratedTeam(r)); return State();
            case "teamExport": return ExportGeneratedTeam(r);
            case "donutRangeInfo": return DonutRangeInfo();
            case "donutRangeGenerate": Mutate(()=>GenerateDonutRange(r));return State();
            case "extraTools": return ExtraTools();
            case "chatterAudio": return new {wav=Convert.ToBase64String(ChatterWave())};
            case "cgearPngExport": return ExportCGearPNG(r);
            case "cgearImage": return ReadCGearImage();
            case "cgearImageSet": Mutate(()=>SetCGearImage(r)); return State();
            case "extraPage": return ReadExtra(S(r,"kind"));
            case "extraEntry": return ReadExtra(S(r,"kind"),S(r,"id")).entries.Single(e=>e.id==S(r,"id"));
            case "extraSet": Mutate(()=>EditExtra(r)); return State();
            case "medalsSetSelected": Mutate(()=>SetSelectedMedals(r));return State();
            case "extraView": Mutate(()=>ViewExtra(r)); return State();
            case "extraExport": return ExportExtra(r);
            case "treats": return Treats(r);
            case "treatsSet": Mutate(()=>EditTreats(r)); return State();
            case "speciesGuide": return SpeciesGuide();
            case "eventWork": return EventWorkRows();
            case "eventWorkSet": Mutate(()=>EditEventWork(r)); return State();
            case "saveBlocks": return SaveBlockList(r);
            case "saveBlock": return SaveBlockDetail(r);
            case "saveBlockSet": Mutate(()=>EditSaveBlock(r)); return State();
            case "saveBlockExport": return ExportSaveBlock(r);
            case "saveBlocksExport": return ExportBlockArchive(r);
            case "saveBlocksReview": return ReviewBlocks(r);
            case "saveBlocksApply": Mutate(()=>ApplyBlockReview(r)); return State();
            case "saveBlocksRawExport": return ExportRawBlocks(r);
            case "state": return State();
            case "dexRecord": return DexRecord(N(r,"species"));
            case "dexRecordGroup": Mutate(()=>EditDexGroup(r)); return State();
            case "dexRecordSet": Mutate(() => EditDexRecord(r)); return State();
            case "suggestMoves": return SuggestMoves(r);
            case "moveChoices": return MoveChoices(r);
            case "pokemonQR": return PokemonQR();
            case "pokemonQRImport": Mutate(()=>ImportPokemonQR(r));return State();
            case "encounterTrainer": return EncounterTrainerInfo(r);
            case "encounterCriteria": return EncounterCriteriaInfo(r);
            case "encounterSearch": return SearchEncounters(r);
            case "encounterPrepare": Mutate(() => PrepareEncounter(r)); return State();
            case "libraryScan": return ScanPokemonLibrary(r);
            case "libraryPrepare": Mutate(() => PrepareLibraryPokemon(r)); return State();
            case "plusRecords": return PlusRecords();
            case "plusRecordsSet": Mutate(() => EditPlusRecords(r)); return State();
            case "boxLayoutSet": Mutate(() => EditBoxLayout(r)); return State();
            case "slotSwap": Mutate(() => SwapBoxSlots(r)); return State();
            case "decorations": return Decorations();
            case "decorationsSet": Mutate(() => EditDecorations(r)); return State();
            case "superTrainingSet": Mutate(() => EditSuperTraining(r)); return State();
            case "moveRecords": return MoveRecords();
            case "moveRecordsSet": Mutate(() => EditMoveRecords(r)); return State();
            case "research": return Research(N(r,"species",25));
            case "researchSet": Mutate(() => EditResearch(r)); return State();
            case "dexDetails": return DexDetails(r);
            case "dexDetailsSet": Mutate(() => EditDexDetails(r)); return State();
            case "giftAlbum": return GiftAlbum();
            case "giftAlbumSet": Mutate(() => EditGiftAlbum(r)); return State();
            case "giftAlbumExport": return ExportAlbumGift(r);
            case "giftsFilter": return FilterGifts(r);
            case "giftsExportSelection": return ExportGiftSelection(r);
            case "storageReportPreview": return StorageReportData(r);
            case "reportColumns": return ReportColumns();
            case "storageSearch": return StorageSearch(r);
            case "gifts": return Gifts();
            case "giftsLoadFolder": return LoadGiftFolder(r);
            case "giftsClearFolders": return ClearGiftFolders();
            case "giftPrepare": Mutate(() => PrepareGift(N(r,"id"))); return State();
            case "giftQR": return GiftQR(r);
            case "giftQRImport": return ImportGiftQR(r);
            case "giftExport": return ExportGift(r);
            case "storage": return StorageRows();
            case "boxExport": return ExportBoxes(r);
            case "boxActions": return BoxActions();
            case "boxPreview": return BoxPreview(r);
            case "storageReport": return ExportStorageReport(r);

            case "saveDiscover": return DiscoverSaves(r);
            case "saveBackups": return ListSaveBackups();
            case "saveBackupExport": return ExportSaveBackup(r);
            case "saveOpenOptions": return SaveOpenOptions();
            case "open":
            {
                var path = Path.GetFullPath(S(r, "path"));
                var info = new FileInfo(path);
                if (info.Length > 64 * 1024 * 1024) throw new Exception("This file is too large to be a supported save or Pokémon file.");
                var bytes = File.ReadAllBytes(path);
                var loadedSave = r.TryGetProperty("saveType",out _) ? OpenWithHandler(bytes,path,r) : SaveUtil.GetSaveFile(bytes, path);
                PKM? loadedEntity = null;
                if (loadedSave == null) loadedEntity = EntityFormat.GetFromBytes(bytes, EntityFileExtension.GetContextFromExtension(Path.GetExtension(path)));
                if (loadedSave == null && loadedEntity == null) throw new Exception("PKHeX could not recognize this file. Open an exported, decrypted game save or an individual Pokémon file.");
                if (loadedSave != null && settings.BackupOnOpen) BackupOriginal(path, bytes);
                var previous=Capture();var oldSource=sourcePath;var oldDemo=demo;var oldRevision=revision;var oldBatch=batchCandidate;var oldUndo=undo.ToArray();var oldRedo=redo.ToArray();
                try {
                    batchCandidate = null; revision++; save = loadedSave; entity = loadedEntity;
                    sourcePath = loadedSave != null ? path : null; entitySourcePath = loadedEntity != null ? path : null;
                    box = 0; slot = -1; party = dirty = pending = demo = false; undo.Clear(); redo.Clear();
                    if (save != null) { ParseSettings.InitFromSaveFileData(save); if (save.BoxCount > 0) Select(0, 0, false); }
                    else ParseSettings.ClearActiveTrainer();
                    return State();
                } catch {
                    Restore(previous);sourcePath=oldSource;demo=oldDemo;revision=oldRevision;batchCandidate=oldBatch;
                    undo.Clear();foreach(var snapshot in oldUndo.Reverse())undo.Push(snapshot);
                    redo.Clear();foreach(var snapshot in oldRedo.Reverse())redo.Push(snapshot);
                    if(save==null)ParseSettings.ClearActiveTrainer();throw;
                }
            }
            case "sampleGames": return SampleGames();
            case "zaEventCompare": return CompareZAEvents(r);
            case "zaEvents": return ReadZAEvents(r);
            case "zaEventSet": Mutate(()=>SetZAEvent(r));return State();
            case "zaEventNames": return LoadZAEventNames(r);
            case "fameNameBytesInfo": return ReadFameNameBytes(r);
            case "fameNameBytesSet": Mutate(()=>SetFameNameBytes(r));return State();
            case "nameBytesInfo": return ReadNameBytes(r);
            case "nameBytesSet": Mutate(()=>SetNameBytes(r));return State();
            case "memoryInfo": return ReadMemoryPage(r);
            case "memorySet": Mutate(()=>SetMemoryPage(r));return State();
            case "cosmeticSet": Mutate(()=>EditCosmetics(r));return State();
            case "demo":
            {
                var version = Enum.Parse<GameVersion>(S(r, "version", "SL"));
                sampleVersion=version;
                batchCandidate = null; revision++; save = BlankSaveFile.Get(version, "Preview");
                InitializeDemoBoxLayout(save);
                if(save is SAV9SV fashionSample)foreach(var category in PaldeaWardrobe) {
                    var data=fashionSample.Blocks.GetBlock(category.key).Data;
                    for(int i=0;i+8<=data.Length;i+=8)new FashionItem9{Value=FashionItem9.None}.Write(data.Slice(i,8));
                }
                if (save is SAV8LA sample)
                {
                    // Only initialize known scalar metadata in the synthetic demo.
                    foreach (var key in new[] { SaveBlockAccessor8LA.KExpeditionTeamRank, SaveBlockAccessor8LA.KSatchelUpgrades })
                        sample.Accessor.GetBlock(key).ChangeStoredType(SCTypeCode.UInt32);
                }
                entity = save.BlankPKM;
                entity.Species = 25; entity.Nickname = "Pikachu"; entity.CurrentLevel = 25;
                entity.OriginalTrainerName = "Preview"; entity.TID16 = save.TID16; entity.SID16 = save.SID16;
                entity.Version = version; entity.RefreshChecksum();
                save.SetBoxSlotAtIndex(entity, 0, 0);
                sourcePath = entitySourcePath = null; box = 0; slot = 0; party = dirty = pending = false; demo = true;
                undo.Clear(); redo.Clear(); ParseSettings.InitFromSaveFileData(save);
                return State();
            }
            case "box": box = Math.Clamp(N(r, "box"), 0, Math.Max(0, RequireSave().BoxCount - 1)); return State();
            case "select": Select(N(r, "box"), N(r, "slot"), B(r, "party")); return State();
            case "entitySet":
                Mutate(() => { SetProperty(RequireEntity(), S(r, "field"), S(r, "value")); RequireEntity().RefreshChecksum(); pending = true; }); return State();
            case "entityEdit":
                Mutate(() => {
                    var pk = RequireEntity();
                    ApplyPokemonEdits(pk,r.GetProperty("edits"));
                    pk.RefreshChecksum(); pending = true;
                    if (B(r, "apply")) {
                        if (r.TryGetProperty("destinationSlot", out _)) {
                            var sav = RequireSave(); int b = N(r,"destinationBox"), s = N(r,"destinationSlot");
                            bool isParty = B(r,"destinationParty");
                            if (pk.Species == 0) throw new Exception("Choose a Pokémon in the editor first.");
                            if (isParty) {
                                b = box; // Party destinations retain the currently displayed box.
                                if (!sav.HasParty || s < 0 || s >= 6 || s > sav.PartyCount) throw new Exception("Choose an occupied party slot or the first empty party slot.");
                            } else ValidateMovableSlot(sav,b,s);
                            box = b; slot = s; party = isParty;
                        }
                        ApplyEntity();
                    }
                }); return State();
            case "entityAction":
                Mutate(() => {
                    var pk = RequireEntity();
                    switch (S(r, "action"))
                    {
                        case "shiny": SetShinyFlag(pk,!pk.IsShiny); break;
                        case "randomIV": pk.SetRandomIVs(); break;
                        case "randomEV":
                            if(pk is IGanbaru or IAwakened) throw new Exception("This game uses grit or awakened values instead of EVs.");
                            Span<int> evs=stackalloc int[6]; EffortValues.SetRandom(evs,pk.Format); pk.SetEVs(evs); break;
                        case "clearIV": pk.IV_HP = pk.IV_ATK = pk.IV_DEF = pk.IV_SPA = pk.IV_SPD = pk.IV_SPE = 0; break;
                        case "maxEV":
                            if(pk is IGanbaru or IAwakened) throw new Exception("This game uses grit or awakened values instead of EVs.");
                            Span<int> maximumEVs=stackalloc int[6]; EffortValues.SetMax(maximumEVs,pk); pk.SetEVs(maximumEVs); break;
                        case "rerollPID":
                            if(pk.Format<3) throw new Exception("This format has no personality ID.");
                            pk.SetPIDGender(pk.Gender); break;
                        case "pokerusNone": case "pokerusInfected": case "pokerusCured":
                            if(!SupportsPokerus(pk)) throw new Exception("This game does not support Pokérus.");
                            var mode=S(r,"action");
                            pk.PokerusStrain=mode=="pokerusNone" ? 0 : Math.Max(1,pk.PokerusStrain);
                            pk.PokerusDays=mode=="pokerusInfected" ? Pokerus.GetMaxDuration(pk.PokerusStrain) : 0; break;
                        case "maxIV": pk.IV_HP = pk.IV_ATK = pk.IV_DEF = pk.IV_SPA = pk.IV_SPD = pk.IV_SPE = pk.MaxIV; break;
                        case "clearEV": pk.EV_HP = pk.EV_ATK = pk.EV_DEF = pk.EV_SPA = pk.EV_SPD = pk.EV_SPE = 0; break;
                        case "heal": pk.ResetPartyStats(); break;
                        case "maxGrit":
                            if (pk is not IGanbaru grit) throw new Exception("This Pokémon format does not use grit values.");
                            for (int i = 0; i < 6; i++) grit.SetGV(i, pk.GetMaxGanbaru(i));
                            break;
                        default: throw new Exception("Unknown edit.");
                    }
                    pk.RefreshChecksum(); pending = true;
                }); return State();
            case "apply":
                Mutate(ApplyEntity); return State();
            case "delete":
                Mutate(() => {
                    var sav = RequireSave();
                    if (slot < 0) throw new Exception("Select a slot first.");
                    if (party) { var data = sav.PartyData.ToList(); if (slot < data.Count) data.RemoveAt(slot); sav.PartyData = data; }
                    else sav.SetBoxSlotAtIndex(sav.BlankPKM, box, slot);
                    entity = sav.BlankPKM; dirty = true; pending = false;
                }); return State();
            case "slotFileImport": Mutate(()=>ImportSlotFile(r));return State();
            case "importEntity":
            {
                var path = Path.GetFullPath(S(r, "path"));
                if (new FileInfo(path).Length > 1024 * 1024) throw new Exception("This is too large to be a Pokémon file.");
                var pk = EntityFormat.GetFromBytes(File.ReadAllBytes(path), EntityFileExtension.GetContextFromExtension(Path.GetExtension(path), save?.Context ?? EntityContext.None)) ?? throw new Exception("Unrecognized Pokémon file.");
                if (save != null && pk.GetType() != save.PKMType)
                    pk = EntityConverter.ConvertToType(pk, save.PKMType, out _) ?? throw new Exception("This Pokémon cannot be converted to the open save's format.");
                Mutate(() => { entity = pk; entitySourcePath = path; pending = true; }); return State();
            }
            case "saveSet":
                Mutate(() => { var sav = RequireSave(); var id = S(r, "field"); if (!SaveFields(sav).Any(f => f.id == id && f.editable)) throw new Exception("This save field is not editable in this build."); SetProperty(sav, id, S(r, "value")); dirty = true; ParseSettings.InitFromSaveFileData(sav); }); return State();
            case "exportSave":
            {
                if (pending) throw new Exception("Apply the Pokémon edits to a slot before exporting the save.");
                if (demo) throw new Exception("The sample is for exploring the editor and cannot be exported as a game save. Open your own save to export changes.");
                var path = ExportPath(S(r, "path"), sourcePath);
                var data = RequireSave().Clone().Write().ToArray();
                var verified = SaveUtil.GetSaveFile(data);
                if (verified == null || !verified.ChecksumsValid) throw new Exception("The edited save could not be reopened with valid checksums. Undo the last edit and try again.");
                AtomicWrite(path, data); dirty = false; undo.Clear(); redo.Clear(); return State();
            }
            case "exportEntity":
            {
                var pk = RequireEntity().Clone(); pk.RefreshChecksum();
                var bytes = new byte[pk.SIZE_PARTY]; pk.WriteDecryptedDataParty(bytes);
                AtomicWrite(ExportPath(S(r, "path"), entitySourcePath), bytes);
                if (save == null) { pending = false; undo.Clear(); redo.Clear(); }
                return State();
            }
            case "showdownTeamPreview": return ShowdownTeamPreview(r);
            case "showdown": return new { text = new ShowdownSet(RequireEntity()).GetText(new BattleTemplateExportSettings(settings.ExportCommunityFormat ? BattleTemplateConfig.CommunityStandard : BattleTemplateConfig.Showdown, settings.ExportLanguage)) };
            case "showdownImport":
                Mutate(() => {
                    var set = new ShowdownSet(S(r, "text"));
                    if (set.Species == 0 || set.Species > RequireEntity().MaxSpeciesID) throw new Exception("The set does not contain a supported species.");
                    if (set.InvalidLines.Count != 0) throw new Exception("Unrecognized Showdown lines: " + string.Join("; ", set.InvalidLines));
                    if(RequireEntity() is GBPKM && !set.Shiny)SetShinyFlag(RequireEntity(),false);
                    RequireEntity().ApplySetDetails(set); RequireEntity().RefreshChecksum(); pending = true;
                }); return State();
            case "folderBatchPreview": return PreviewFolderBatch(r);
            case "folderBatchExport": return ExportFolderBatch(r);
            case "batchPreview": return BatchPreview(S(r,"text"), S(r,"scope","box"));
            case "batchApply":
                if (batchCandidate == null || batchToken != S(r,"token") || revision != batchRevision) throw new Exception("The save changed after this preview. Preview the batch again.");
                var candidate = batchCandidate;
                Mutate(() => { save = candidate; dirty = true; if (slot >= 0) Select(box,slot,party); });
                return State();
            case "boxRename":
                Mutate(() => EditBoxLayout(r)); return State();
            case "saveObject": return SaveObject(S(r, "path"));
            case "objectSet":
                Mutate(() => {
                    var sav = RequireSave(); var id = S(r, "field");
                    var containerPath = id.Contains('.') ? id[..id.LastIndexOf('.')] : "";
                    var root = Resolve(sav, containerPath);
                    if (!Fields(root, "Save", containerPath.Length == 0 ? "" : containerPath + ".").Any(f => f.id == id && f.editable)) throw new Exception("This field is not editable.");
                    if (containerPath.Length == 0 && !SaveFields(sav).Any(f => f.id == id && f.editable)) throw new Exception("This field is managed by the engine.");
                    var before = sav.Clone().Write().ToArray();
                    SetProperty(sav, id, S(r, "value"));
                    var after = sav.Clone().Write().ToArray();
                    if (before.AsSpan().SequenceEqual(after)) throw new Exception("This property does not persist to the save. No edit was applied.");
                    dirty = true; ParseSettings.InitFromSaveFileData(sav);
                });
                return SaveObject(S(r, "path"));
            case "eventResearch": return EventResearchRows();
            case "eventResearchSet": Mutate(()=>SetEventResearch(r)); return State();
            case "eventCompare": return CompareEvents(r);
            case "eventCompareExport": return ExportEventComparison(r);
            case "events": return Events();
            case "eventSet":
                Mutate(() => {
                    var flags = EventFlags(RequireSave()) ?? throw new Exception("Event flags are unavailable for this save.");
                    int index = N(r, "index"); if (index < 0 || index >= flags.EventFlagCount) throw new Exception("Invalid flag index.");
                    flags.SetEventFlag(index, B(r, "value")); if(EventWorkTarget(RequireSave()) is EventWork7 alola)alola.UpdateQrConstants(); dirty = true;
                }); return Events();
            case "undo": if (undo.Count > 0) { redo.Push(Capture()); Restore(undo.Pop()); revision++; batchCandidate = null; } return State();
            case "redo": if (redo.Count > 0) { undo.Push(Capture()); Restore(redo.Pop()); revision++; batchCandidate = null; } return State();
            case "inventory": return Inventory();
            case "inventoryAdd": Mutate(() => AddInventory(r)); return Inventory();
            case "inventorySet":
                Mutate(() => {
                    var sav = RequireSave(); var bag = sav.Inventory;
                    var pouch = bag.Pouches[N(r, "pouch")]; var item = pouch.Items[N(r, "slot")];
                    int id = N(r, "item"), count = N(r, "count");
                    if (pouch is InventoryPouch9 or InventoryPouch9a && id != item.Index) throw new Exception("This game uses fixed item rows. Find the desired item's row and edit its quantity.");
                    if (id != 0 && id != item.Index && pouch.Items.Any(x=>x != item && x.Index == id)) throw new Exception("This item already has a row in the pouch. Edit that row's quantity instead.");
                    if (id != 0 && !bag.Info.GetItems(pouch.Type).Contains((ushort)id)) throw new Exception("This item does not belong in this pouch.");
                    if (id < 0 || id > sav.MaxItemID || count < 0 || count > bag.GetMaxCount(pouch.Type, id)) throw new Exception("Item or quantity is outside this pouch's limits.");
                    item.Index = id; item.Count = id == 0 ? 0 : count; bag.CopyTo(sav); dirty = true;
                }); return Inventory();
            case "inventoryFields": return InventoryFields(N(r,"pouch"), N(r,"slot"));
            case "inventoryFieldSet":
                Mutate(() => {
                    var sav = RequireSave(); var bag = sav.Inventory; var item = bag.Pouches[N(r,"pouch")].Items[N(r,"slot")];
                    var id = S(r,"field");
                    if (id is "Index" or "Count") throw new Exception("Use the inventory row to change item IDs and quantities.");
                    if (item.Index == 0) throw new Exception("Add an item to this slot before changing its flags.");
                    SetProperty(item, id, S(r,"value")); var expected = Property(item,id)!.GetValue(item);
                    bag.CopyTo(sav);
                    var persisted = sav.Inventory.Pouches[N(r,"pouch")].Items.FirstOrDefault(x=>x.Index == item.Index);
                    if (persisted == null || !Equals(Property(persisted,id)?.GetValue(persisted),expected)) throw new Exception("The game's inventory rules do not permit this flag value for the item. No change was applied.");
                    dirty = true;
                }); return InventoryFields(N(r,"pouch"), N(r,"slot"));
            case "slotPreview": return SlotPreview(r);
            case "wardrobeSet": Mutate(()=>ChangeWardrobe(r)); return State();
            case "fashion": return Fashion();
            case "fashionUnlock": Mutate(()=>UnlockFashion()); return State();
            case "fashionSet": Mutate(()=>EditFashion(r)); return Fashion();
            case "inventoryGiveAll": Mutate(()=>GiveInventory(r)); return State();
            case "dexBulkOptions": return DexBulkOptions();
            case "dexBulk": Mutate(()=>EditDexBulk(r)); return State();
            case "dexGiveAll": Mutate(()=>GiveDex()); return State();
            case "dex": return Dex();
            case "dexSet":
                Mutate(() => {
                    var sav = RequireSave(); var id = N(r, "species");
                    if (id < 1 || id > sav.MaxSpeciesID) throw new Exception("Invalid species.");
                    if (!SupportsDex(sav)) throw new Exception("This save does not expose the basic Pokédex editor.");
                    SetDexFlags(sav,(ushort)id,B(r,"seen") || B(r,"caught"),B(r,"caught")); dirty = true;
                }); return Dex();
            case "lookup": return Lookup(S(r, "kind"));
            case "donutClipboard": {var sav=RequireSave() as SAV9ZA ?? throw new Exception("Donuts are unavailable.");int i=N(r,"id");if(i<0||i>=DonutPocket9a.MaxCount)throw new Exception("Choose a donut slot.");return Convert.ToHexString(sav.Donuts.GetDonut(i).Data);}
            case "trainerNameSupported": return TrainerNameSupported();
            case "baseNameInfo":return ReadBaseName(r);
            case "baseNameSet":Mutate(()=>SetBaseName(r));return State();
            case "trainerNameInfo": return TrainerNameInfo(r);
            case "trainerNameSet": Mutate(()=>SetTrainerName(r));return State();
            case "trainerPhotos": return TrainerPhotos();
            case "trainerDetails": return TrainerDetailFields();
            case "trainerDetailSet":
                if (!TrainerDetailFields().Any(f => f.id == S(r,"field"))) throw new Exception("This trainer field is unavailable for the loaded game.");
                Mutate(() => SetTrainerDetail(r)); return State();
            case "settings": return SettingsFields();
            case "settingsSet":
            {
                var clone = JsonSerializer.Deserialize<EditorSettings>(JsonSerializer.Serialize(settings))!;
                SetProperty(clone, S(r, "field"), S(r, "value"));
                if (settingsPath != null) { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); AtomicWrite(settingsPath, System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clone, jsonOptions))); }
                settings = clone; settings.Apply(); strings = GameInfo.GetStrings(settings.CatalogLanguage); GameInfo.Strings = strings; GameInfo.CurrentLanguage = settings.CatalogLanguage; return SettingsFields();
            }
            default: throw new Exception("Unknown command.");
        }
    }

    void ApplyEntity()
    {
        var sav = RequireSave(); var pk = RequireEntity();
        if (slot < 0) throw new Exception("Select a destination slot first.");
        if (party) sav.SetPartySlotAtIndex(pk, slot); else sav.SetBoxSlotAtIndex(pk, box, slot);
        entity = party ? sav.GetPartySlotAtIndex(slot) : sav.GetBoxSlotAtIndex(box, slot);
        dirty = true; pending = false;
    }
    void Select(int b, int s, bool p)
    {
        var sav = RequireSave();
        if (p ? !sav.HasParty || s < 0 || s >= 6 : b < 0 || b >= sav.BoxCount || s < 0 || s >= sav.BoxSlotCount) throw new Exception("Invalid slot.");
        entity = p ? sav.GetPartySlotAtIndex(s) : sav.GetBoxSlotAtIndex(b, s);
        box = b; slot = s; party = p; pending = false; entitySourcePath = null;
    }
    static void AtomicWrite(string path, byte[] bytes)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static string ExportPath(string path, string? original)
    {
        path = Path.GetFullPath(path);
        if (original != null && string.Equals(path, original, StringComparison.OrdinalIgnoreCase)) throw new Exception("Choose a new filename. This preview keeps the opened original unchanged.");
        if (File.Exists(path) && new FileInfo(path).LinkTarget != null) throw new Exception("Export to a regular file, not a symbolic link.");
        return path;
    }
    string Species(ushort id) => id < strings.specieslist.Length ? strings.specieslist[id] : $"Species {id}";
    static string Portrait(PKM pk) => $"{pk.Context}:{pk.Species}:{pk.Form}:{pk.Gender}:{(pk is IFormArgument arg ? arg.FormArgument : 0)}:{(pk.IsShiny ? 1 : 0)}";
    static string Sprite(PKM pk) => SpriteFor(pk.Species,pk.Form,pk.Gender,pk is IFormArgument a ? a.FormArgument : 0,pk.Context,pk.IsShiny);
    static string SpriteFor(ushort species,byte form,byte gender,uint argument,EntityContext context,bool shiny=false)
    {
        string name=species==982 && form==1 ? "b_982-1" : "b_"+SpriteName.GetResourceStringSprite(species,form,gender,argument,context)[1..].Replace('_','-');
        return shiny && species != 0 ? name+"s" : name;
    }
    object SlotInfo(PKM pk, int index, bool isParty) => new { heldItem=pk.Species==0 ? 0:pk.HeldItem, heldItemIcon=pk.Species==0 || pk.HeldItem==0 ? "":ItemIcon(pk.HeldItem,pk.Context), heldItemName=pk.Species==0 || pk.HeldItem==0 ? "":MoveLabel(strings.GetItemStrings(pk.Context,pk.Version),pk.HeldItem), pokemonData=pk.Species==0 ? "":PokemonBytes(pk), pokemonExtension=pk.Extension, journalKey=JournalKey(pk), sprite = Sprite(pk), portrait = Portrait(pk), index, party = isParty, species = pk.Species, name = Species(pk.Species), nickname = pk.Nickname, level = pk.CurrentLevel, shiny = pk.IsShiny, empty = pk.Species == 0, gender=pk.Gender, alpha=pk is IAlphaReadOnly {IsAlpha:true}, egg=pk.IsEgg };
    object State(string suggestionMessage = "")
    {
        var slots = new List<object>(); var partySlots = new List<object>();
        if (save != null)
        {
            if (save.BoxCount > 0) for (int i = 0; i < save.BoxSlotCount; i++) slots.Add(SlotInfo(save.GetBoxSlotAtIndex(box, i), i, false));
            if (save.HasParty) for (int i = 0; i < 6; i++) partySlots.Add(SlotInfo(save.GetPartySlotAtIndex(i), i, true));
        }
        string report = "", legality = "empty";
        LegalityAnalysis? moveAnalysis = null;
        if (entity is { Species: > 0 })
        {
            try { var analysis = new LegalityAnalysis(entity); moveAnalysis = analysis; report = analysis.Report(true); legality = analysis.Valid ? "valid" : "invalid"; }
            catch (Exception ex) { legality = "unknown"; report = "Legality analysis could not complete: " + ex.Message; }
        }
        return new {
            growth = GrowthInfo(), abilityDescription = AbilityDescription(),
            engineVersion = typeof(PKM).Assembly.GetName().Version?.ToString() ?? "unknown",
            loaded = save != null || entity != null, hasSave = save != null, demo, dirty, pending,
            sourceName = sourcePath != null ? Path.GetFileName(sourcePath) : entitySourcePath != null ? Path.GetFileName(entitySourcePath) : demo ? "Sample workspace" : "",
            game = save != null ? GameInfo.GetVersionName(demo ? sampleVersion:save.Version) : entity != null ? GameInfo.GetVersionName(entity.Version) : "",
            gameVersion = save != null ? (demo ? sampleVersion:save.Version).ToString():"",
            gameLogoVersion=save is SAV4Ranch ? "RANCH" : save!=null?(demo?sampleVersion:save.Version).ToString():"",
            moveChecks = CheckMoves(moveAnalysis),
            generation = save?.Generation ?? entity?.Format ?? 0,
            boxCount = save?.BoxCount ?? 0, box, slot, party, slots, partySlots,
            boxLayout = BoxLayout(), revision, dragSession,
            boxNames = save == null ? [] : Enumerable.Range(0, save.BoxCount).Select(i => save is IBoxDetailName named ? named.GetBoxName(i) : $"Box {i+1}").ToArray(),
            entityName = entity == null || entity.Species == 0 ? "Empty slot" : Species(entity.Species),
            trainerBadges=TrainerBadges(), trainerJourney=JourneyProgress(), entityData=entity is {Species:>0} ? PokemonBytes(entity):"", entityJournalKey=JournalKey(entity), entityPortrait=entity==null?null:Portrait(entity), entitySprite = entity == null ? "b_0" : Sprite(entity), entityNickname = entity?.Nickname ?? "", entityExtension = entity?.Extension ?? "pk9", entityLevel = entity?.CurrentLevel ?? 0,
            stats = entity == null ? [] : entity.GetStats(entity.PersonalInfo),
            baseStats = entity == null ? Array.Empty<int>() : Enumerable.Range(0,6).Select(i=>entity.PersonalInfo.GetBaseStatValue(i)).ToArray(),
            potential = entity?.PotentialRating ?? 0, cosmeticInfo=CosmeticInfo(),
            decorations = entity == null ? null : Decorations(),
            superTraining = SuperTraining(),
            canTreats=TreatCollections().Length>0, canFashion = FashionSupported(), characteristic = CharacteristicText(), originGame = entity == null ? "" : GameInfo.GetVersionName(entity.Version), originVersion = entity?.Version.ToString() ?? "",
            canDexRecords = LegacyDex(save) || save is SAV8BS or SAV9SV,
            canPlusRecords = entity is IPlusRecord, canMoveRecords = entity is ITechRecord or IMoveShop8, canResearch = save is SAV8LA, canGiftAlbum = save is IMysteryGiftStorageProvider,
            fields = entity == null ? [] : EntityFields(entity), saveFields = save == null ? [] : SaveFields(save),
            suggestionMessage, legality, report, canUndo = undo.Count > 0, canRedo = redo.Count > 0,
            checksumValid = demo ? false : save?.ChecksumsValid ?? true,
            canEvents = save != null && SupportsEventResearch(save), canDex = save != null && SupportsDex(save), canInventory = SupportsInventory()
        };
    }

    bool SupportsInventory()
    {
        // Blank exploration saves can lack typed inventory metadata.
        try { return save?.Inventory.Pouches.Count > 0; }
        catch (ArgumentException) { return false; }
    }

    Field[] SettingsFields() => Fields(settings, "Preferences", depth: 3).Where(f => f.id != "SlotWrite.ModifyUnset" && f.id is not ("BoxExport.Scope" or "BoxExport.Notify" or "BoxExport.EmptySlots")).Select(f => f.id switch {
        "CatalogLanguage" or "ExportLanguage" => f with { label = f.id == "CatalogLanguage" ? "Game data language" : "Battle template language", kind = "enum", choices = new[] { ("en","English"),("ja","日本語"),("fr","Français"),("it","Italiano"),("de","Deutsch"),("es","Español"),("es-419","Español (Latinoamérica)"),("ko","한국어"),("zh-Hans","简体中文"),("zh-Hant","繁體中文") }.Select(x => new Choice(x.Item1,x.Item2)).ToArray(), help = f.id == "CatalogLanguage" ? "Names of Pokémon, moves, items, games and locations. KeepSake’s interface remains English." : "Language used when copying a battle template." },
        "BackupOnOpen" => f with { label = "Back up saves when opening", help = "Keep original snapshots in Settings → Files & Startup." },
        "ExportCommunityFormat" => f with { label = "Use community battle template format", help = "Uses PKHeX’s community ordering when copying a set. Turn off for standard Showdown format." },
        _ => f
    }).ToArray();
    static readonly string[] mainOrder = ["Species", "Nickname", "IsNicknamed", "CurrentLevel", "EXP", "Gender", "Form", "Ability", "AbilityNumber", "HeldItem", "IsEgg", "PID", "EncryptionConstant"];
    Field[] EntityFields(PKM pk) => Fields(pk, "Advanced").Select(f => f with {
        group = Group(f.id), editable = f.editable && !hiddenEntity.Contains(f.id), lookup = LookupName(f.id)
    }).OrderBy(f => Array.IndexOf(mainOrder, f.id) is var n && n >= 0 ? n : 1000).ToArray();
    Field[] SaveFields(SaveFile sav) => Fields(sav, "Advanced save").Select(f => f with {
        group = trainerFields.Contains(f.id) ? "Trainer" : "Advanced save",
        editable = f.editable && !unsafeSave.Contains(f.id) && Property(sav, f.id)?.GetMethod?.DeclaringType != typeof(SaveFile) || trainerFields.Contains(f.id) && f.editable && IsRealSaveField(sav, f.id)
    }).ToArray();
    static bool IsRealSaveField(SaveFile sav, string id)
    {
        if (id is "DisplayTID" or "DisplaySID") return true;
        return Property(sav, id)?.GetMethod?.DeclaringType != typeof(SaveFile);
    }
    static bool SupportsDex(SaveFile sav) => sav is SAV4 or SAV5 or SAV7 or SAV7b or SAV9SV or SAV8BS or SAV8SWSH or SAV9ZA or SAV8LA || sav.GetType().GetMethod("SetSeen")?.DeclaringType != typeof(SaveFile) && sav.GetType().GetMethod("SetCaught")?.DeclaringType != typeof(SaveFile);
    static string Group(string id)
    {
        if (id.StartsWith("Ribbon") || id.StartsWith("Mark") || id.Contains("Ribbon")) return "Ribbons";
        if (id.StartsWith("Move") || id.StartsWith("Relearn")) return "Moves";
        if (id.StartsWith("IV_") || id.StartsWith("EV_") || id.StartsWith("Stat_") || id.StartsWith("HT_") || id.Contains("HyperTrain") || id.StartsWith("AV_") || id.StartsWith("GV_") || id.Contains("Nature") || id == "Characteristic") return "Stats";
        if (id.StartsWith("Met") || id.StartsWith("Egg") || id is "Ball" or "Version" or "FatefulEncounter" or "GroundTile") return "Met";
        if (id.Contains("Trainer") || id.StartsWith("TID") || id.StartsWith("SID") || id.StartsWith("Handling") || id is "Language" or "CurrentHandler" or "ID32" || id.Contains("Memory") || id.Contains("Friendship")) return "Trainer";
        if (id is "Species" or "Nickname" or "IsNicknamed" or "IsEgg" or "Gender" or "Form" or "Ability" or "AbilityNumber" or "HeldItem" or "CurrentLevel" or "EXP" or "PID" or "EncryptionConstant" or "IsShiny") return "Main";
        return "Advanced";
    }
    static string? LookupName(string id) => id switch {
        "Species" => "species", "HeldItem" => "items", "Ability" => "abilities", "Nature" or "StatNature" => "natures", "Ball" => "balls", "Version" => "games", "MetLocation" => "met", "EggLocation" => "eggMet",
        "Move1" or "Move2" or "Move3" or "Move4" or "RelearnMove1" or "RelearnMove2" or "RelearnMove3" or "RelearnMove4" => "moves", _ => null
    };
    static string Label(string id) => Regex.Replace(id.Replace("_", " "), "(?<=[a-z0-9])([A-Z])", " $1");
    static bool Scalar(Type t) { t = Nullable.GetUnderlyingType(t) ?? t; return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateOnly) || t == typeof(DateTime); }
    static string ValueText(object? value) => value switch { null => "", bool v => v ? "true" : "false", DateOnly d => d.ToString("yyyy-MM-dd"), DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "", _ => value.ToString() ?? "" };
    static int InheritanceDepth(Type? type) => type == null ? 0 : 1 + InheritanceDepth(type.BaseType);
    static IEnumerable<PropertyInfo> Properties(object obj) => obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .GroupBy(p=>p.Name).Select(g=>g.OrderByDescending(p=>InheritanceDepth(p.DeclaringType)).First()).OrderBy(p=>p.Name);
    static PropertyInfo? Property(object obj, string name) => Properties(obj).FirstOrDefault(p=>p.Name == name);
    static Field[] Fields(object obj, string group, string prefix = "", int depth = 0)
    {
        var result = new List<Field>();
        foreach (var p in Properties(obj))
        {
            if (p.GetIndexParameters().Length != 0 || p.GetMethod?.IsPublic != true || p.PropertyType.IsByRefLike || p.PropertyType.IsPointer) continue;
            object? value; try { value = p.GetValue(obj); } catch { continue; }
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            var id = prefix + p.Name;
            if (Scalar(t))
            {
                var choices = t.IsEnum ? Enum.GetNames(t).Select(n => new Choice(n,n)).ToArray() : [];
                var setterBody = p.SetMethod?.GetMethodBody()?.GetILAsByteArray();
                var noOpSetter = setterBody != null && setterBody.All(b => b is 0x00 or 0x2A);
                result.Add(new(id, Label(p.Name), group, ValueText(value), t == typeof(bool) ? "bool" : t == typeof(string) ? "string" : t.IsEnum ? "enum" : t == typeof(DateTime) ? "datetime" : t == typeof(DateOnly) ? "date" : "number", p.SetMethod?.IsPublic == true && !noOpSetter,
                    p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? (value is DateTime date && date.Kind == DateTimeKind.Utc ? "UTC · YYYY-MM-DD HH:mm:ss" : ""), null, choices));
            }
            else if (depth > 0 && value != null && t.Namespace == "PKHeX.Core" && !t.IsValueType)
                result.AddRange(Fields(value, prefix == "" ? Label(p.Name) : group + " / " + Label(p.Name), id + ".", depth - 1));
        }
        return result.ToArray();
    }
    void SetProperty(object root, string path, string input)
    {
        object obj = root;
        var parts = path.Split('.');
        foreach (var name in parts.SkipLast(1)) obj = Child(obj, name);
        var p = Property(obj, parts[^1]) ?? throw new Exception("Unknown field.");
        if (p.SetMethod?.IsPublic != true || !Scalar(p.PropertyType)) throw new Exception("This field is read-only.");
        var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        object? value;
        if (input == "" && Nullable.GetUnderlyingType(p.PropertyType) != null) value = null;
        else if (t.IsEnum)
        {
            value = Enum.Parse(t, input);
            if (root is ITeraType && path is "TeraTypeOriginal" or "TeraTypeOverride")
            {
                // Tera types also use Stellar and an unchanged sentinel outside MoveType.
                int number = Convert.ToInt32(value);
                bool valid = number is >= 0 and <= byte.MaxValue && (path == "TeraTypeOverride"
                    ? TeraTypeUtil.IsValid((byte)number) : TeraTypeUtil.IsOverrideValid((byte)number));
                if (!valid) throw new Exception("Choose a supported Tera type.");
            }
            else if (!Enum.IsDefined(t, value)) throw new Exception("Select a valid value.");
        }
        else if (t == typeof(DateOnly)) value = DateOnly.ParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        else if (t == typeof(DateTime)) value = DateTime.ParseExact(input, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None);
        else value = Convert.ChangeType(input, t, CultureInfo.InvariantCulture);
        if (root is EditorSettings && path == "EncounterResultLimit" && Convert.ToInt32(value) is <1 or >10000) throw new Exception("Choose a result limit from 1 to 10,000.");
        if (root is EditorSettings && path is "CatalogLanguage" or "ExportLanguage" && !GameLanguage.IsLanguageValid(input))
            throw new Exception("Choose a supported game-data language.");
        if (root is PKM pk)
        {
            if (hiddenEntity.Contains(path)) throw new Exception("This field is managed by the engine.");
            double? n = value is IConvertible && t != typeof(string) && t != typeof(bool) ? Convert.ToDouble(value) : null;
            double? max = path switch { "Species" => pk.MaxSpeciesID, "HeldItem" => pk.MaxItemID, "Ability" => pk.MaxAbilityID, "CurrentLevel" => 100, "Gender" => 2, "Form" => 255, "Ball" => pk.MaxBallID, _ when path.StartsWith("GV_") => 10, _ when path.EndsWith("_PPUps") => 3, _ when path.EndsWith("_PP") => 255, _ when path.StartsWith("IV_") => pk.MaxIV, _ when path.StartsWith("EV_") => pk.MaxEV, _ when Regex.IsMatch(path, "^(Move|RelearnMove)[1-4]$") => pk.MaxMoveID, _ => null };
            if (max != null && (n < 0 || n > max) || path == "CurrentLevel" && n < 1) throw new Exception($"{Label(path)} is outside the supported range.");
            if (path == "Nickname" && input.Length > pk.MaxStringLengthNickname) throw new Exception("Nickname is too long for this format.");
            if (path == "OriginalTrainerName" && input.Length > pk.MaxStringLengthTrainer) throw new Exception("Trainer name is too long for this format.");
        }
        if (root is SaveFile sav)
        {
            if (path == "Gender" && Convert.ToInt32(value) is <0 or >1) throw new Exception("Choose a supported trainer gender.");
            if (path == "Language" && !GameInfo.LanguageDataSource(sav.Generation,sav.Context).Any(x=>x.Value==Convert.ToInt32(value))) throw new Exception("Choose a language supported by this game.");
            if (path == "OT" && input.Length > sav.MaxStringLengthTrainer) throw new Exception("Trainer name is too long for this game.");
            if (path == "Money" && Convert.ToUInt64(value) > (ulong)sav.MaxMoney) throw new Exception($"Maximum money is {sav.MaxMoney}.");
            if (path is "PlayedMinutes" or "PlayedSeconds" && (Convert.ToInt32(value) < 0 || Convert.ToInt32(value) > 59)) throw new Exception("Enter a value from 0 to 59.");
        }
        var before = p.GetValue(obj);
        if (before is DateTime existingDate && value is DateTime changedDate) value = DateTime.SpecifyKind(changedDate, existingDate.Kind);
        p.SetValue(obj, value);
        if (root is SAV8SWSH swGender && path == "Gender" && !Equals(before,value)) swGender.MyStatus.GenderAppearance=swGender.Gender;
        if (root is SAV6 gen6 && path == "Gender" && !Equals(before, value)) gen6.Overworld.ResetPlayerModel();
        if (root is SAV9ZA za && path == "Gender" && !Equals(before, value)) za.PlayerFashion.Reset();
        if (!Equals(before, value) && Equals(before, p.GetValue(obj)))
            throw new Exception($"This format did not accept the {Label(path)} change. The field may be constrained by another value, such as egg status.");
    }
    object Lookup(string kind)
    {
        if (kind == "entityAbilities") return AbilityChoices(entity);
        if (kind == "forms" && entity != null)
            return FormConverter.GetFormList(entity.Species, strings.Types, strings.forms, entity.Context).Select((name, i) => new Choice(i.ToString(), string.IsNullOrWhiteSpace(name) ? "Normal" : name)).ToArray();
        if ((kind == "met" || kind == "eggMet") && entity != null)
            return GameInfo.GetLocationList(entity.Version, entity.Context, kind == "eggMet").Select(x => new Choice(x.Value.ToString(), x.Text)).ToArray();
        if (kind == "games") return Enum.GetValues<GameVersion>().Where(v => v.IsValidSavedVersion()).Select(v => new Choice(v.ToString(), GameInfo.GetVersionName(v))).ToArray();
        var list = kind switch { "species" or "journalSpecies" => strings.specieslist, "items" => strings.GetItemStrings(save?.Context ?? entity?.Context ?? EntityContext.Gen9, save?.Version ?? entity?.Version ?? GameVersion.Any), "abilities" => strings.abilitylist, "natures" => strings.natures, "balls" => strings.balllist, "moves" or "giftMoves" => strings.movelist, _ => Array.Empty<string>() };
        var max = kind switch { "species" => entity?.MaxSpeciesID ?? save?.MaxSpeciesID ?? list.Length - 1, "items" => entity?.MaxItemID ?? save?.MaxItemID ?? list.Length - 1, "abilities" => entity?.MaxAbilityID ?? save?.MaxAbilityID ?? list.Length - 1, "moves" => entity?.MaxMoveID ?? save?.MaxMoveID ?? list.Length - 1, _ => list.Length - 1 };
        return list.Take(max + 1).Select((x,i) => new Choice(kind == "natures" ? ((Nature)i).ToString() : i.ToString(), string.IsNullOrWhiteSpace(x) ? $"{(kind=="items"?"Item":"Entry")} {i}" : x)).ToArray();
    }
    object BatchPreview(string text, string scope)
    {
        batchCandidate = null;
        if (pending) throw new Exception("Apply or discard the Pokémon edits before using the batch editor.");
        if (scope is not ("box" or "boxes" or "party")) throw new Exception("Invalid batch scope.");
        text = text.Trim();
        if (text.Length == 0 || text.Length > 100_000 || StringInstructionSet.HasEmptyLine(text)) throw new Exception("Enter batch instructions without blank lines.");
        var sets = StringInstructionSet.GetBatchSets(text.AsSpan());
        if (sets.Any(s=>s.Instructions.Count == 0)) throw new Exception("Each instruction set needs at least one modification.");
        foreach (var set in sets) { EntityBatchEditor.ScreenStrings(set.Filters); EntityBatchEditor.ScreenStrings(set.Instructions); }
        var target = RequireSave().Clone(); var changes = new List<object>(); var errors = new List<string>();
        void Process(PKM pk, int b, int i, bool inParty)
        {
            if (pk.Species == 0) return;
            if (!inParty && target.GetBoxSlotFlags(b,i).IsOverwriteProtected()) return;
            var before = EntityFields(pk).Where(f=>f.editable).ToDictionary(f=>f.id,f=>f.value);
            bool changed = false;
            foreach (var set in sets)
            {
                var result = ModifyWithSlotFilters(new SlotCache(inParty ? new SlotInfoParty(i) : new SlotInfoBox(b,i,target),pk,target),set);
                if ((result & ModifyResult.Error) != 0) { errors.Add($"{(inParty ? "Party" : $"Box {b+1}")}, slot {i+1}: a filter or instruction failed."); return; }
                changed |= result == ModifyResult.Modified;
            }
            if (!changed) return;
            var diff = EntityFields(pk).Where(f=>f.editable && before.TryGetValue(f.id,out var old) && old != f.value).Select(f=>$"{f.label}: {before[f.id]} → {f.value}").ToArray();
            if (diff.Length == 0) return;
            pk.RefreshChecksum();
            var options = default(EntityImportSettings) with { UpdateRecord = EntityImportOption.Disable };
            if (inParty) target.SetPartySlotAtIndex(pk,i,options); else target.SetBoxSlotAtIndex(pk,b,i,options);
            changes.Add(new { location = inParty ? $"Party · slot {i+1}" : $"Box {b+1} · slot {i+1}", name=Species(pk.Species), detail=string.Join("; ",diff) });
        }
        if (scope == "party") for (int i=0;i<target.PartyCount;i++) Process(target.GetPartySlotAtIndex(i),0,i,true);
        else for (int b=0;b<target.BoxCount;b++) { if (scope == "box" && b != box) continue; for (int i=0;i<target.BoxSlotCount;i++) Process(target.GetBoxSlotAtIndex(b,i),b,i,false); }
        batchToken = Guid.NewGuid().ToString("N"); batchRevision = revision;
        if (errors.Count == 0 && changes.Count > 0) batchCandidate = target;
        return new { token=batchToken, count=changes.Count, changes, errors };
    }
    static readonly HashSet<string> omittedNodes = new(StringComparer.Ordinal) { "Personal", "Metadata", "State", "BlankPKM", "Inventory", "Accessor", "Blocks", "PKMType", "AllBlocks", "PartyData" };
    static object Child(object obj, string key)
    {
        if(key.StartsWith("@item:") && obj is System.Collections.IList list) {
            if(!int.TryParse(key[6..],out int i)||i<0||i>=list.Count||i>=10000)throw new Exception("Invalid collection index.");
            var item=list[i];if(item==null||item is PKM or SaveFile||item.GetType().Namespace!="PKHeX.Core")throw new Exception("This collection entry is not an editable save structure.");return item;
        }
        if (key.StartsWith("@raid:") && obj is RaidSpawnList9 raids)
        {
            int index = int.Parse(key[6..]);
            if (index < 0 || index >= raids.CountAll) throw new Exception("Invalid raid index.");
            return raids.GetRaid(index);
        }
        if (omittedNodes.Contains(key)) throw new Exception("This internal object is not available for editing.");
        return Property(obj, key)?.GetValue(obj) ?? throw new Exception("Unknown save object.");
    }
    static object Resolve(object root, string path)
    {
        if (path.Length == 0) return root;
        if (path.Split('.').Length > 8) throw new Exception("Maximum object depth reached.");
        foreach (var key in path.Split('.')) root = Child(root,key);
        return root;
    }
    object SaveObject(string path)
    {
        var sav = RequireSave(); var obj = Resolve(sav,path); var nodes = new List<object>();
        if(obj is System.Collections.IList list)for(int i=0;i<Math.Min(list.Count,10000);i++) {
            var item=list[i];if(item!=null && item is not (PKM or SaveFile) && item.GetType().Namespace=="PKHeX.Core")nodes.Add(new{id=path+".@item:"+i,label=$"Entry {i+1}",type=item.GetType().Name});
        }
        if (obj is RaidSpawnList9 raids)
            for (int i=0; i<raids.CountAll; i++) nodes.Add(new { id = path + ".@raid:" + i, label = $"Raid {i+1}", type = "TeraRaidDetail" });
        foreach (var p in Properties(obj))
        {
            if (omittedNodes.Contains(p.Name) || p.GetIndexParameters().Length != 0 || p.GetMethod?.IsPublic != true || p.PropertyType.IsByRefLike || p.PropertyType.IsPointer || Scalar(p.PropertyType)) continue;
            object? value; try { value = p.GetValue(obj); } catch { continue; }
            if (value == null || value is SaveFile or PKM) continue;
            bool collection=value is System.Collections.IList items && items.Count>0 && items.Count<=10000 && items[0] is object first && first is not (PKM or SaveFile) && first.GetType().Namespace=="PKHeX.Core";
            if(value.GetType().Namespace!="PKHeX.Core" && !collection)continue;
            nodes.Add(new { id = (path.Length == 0 ? "" : path + ".") + p.Name, label = Label(p.Name), type = value.GetType().Name });
        }
        return new { path, title = path.Length == 0 ? "Save overview" : Label(path.Split('.').Last()), type = obj.GetType().Name,
            fields = path.Length == 0 ? SaveFields(sav) : Fields(obj,"Save",path + "."), nodes };
    }
    static IEventFlagArray? EventFlags(SaveFile sav) => sav is IEventFlagArray flags ? flags : sav is IEventFlagProvider37 provider ? provider.EventWork : null;
    object Events()
    {
        var flags = EventFlags(RequireSave());
        return new { supported = flags != null, entries = flags == null ? [] : Enumerable.Range(0,flags.EventFlagCount).Select(i=>new { id=i, value=flags.GetEventFlag(i) }).ToArray() };
    }
    Field[] InventoryFields(int pouch, int slot) => Fields(RequireSave().Inventory.Pouches[pouch].Items[slot],"Item").Where(f=> f.id is not ("Index" or "Count" or "Pouch" or "SortOrder" or "Flags" or "Padding")).ToArray();
    object Inventory()
    {
        var sav = RequireSave(); var bag = sav.Inventory;
        var names = strings.GetItemStrings(sav.Context, sav.Version);
        return new { pouches = bag.Pouches.Select((p,pi) => new { id = pi, name = p.Type == InventoryType.Candy && sav.Context==EntityContext.Gen9 ? "TM Materials" : p.Type.ToString(), max = p.MaxCount, fixedItems = p is InventoryPouch9 or InventoryPouch9a,
            limits = bag.Info.GetItems(p.Type).ToArray().Distinct().ToDictionary(i=>i.ToString(), i=>bag.GetMaxCount(p.Type,i)),
            choices = bag.Info.GetItems(p.Type).ToArray().Prepend((ushort)0).Distinct().Select(i => new Choice(i.ToString(), i < names.Length && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : $"Item {i}")).ToArray(),
            items = p.Items.Select((item,si) => new { id = si, item = item.Index, count = item.Count, icon = ItemIcon(item.Index,sav.Context), name = item.Index >= 0 && item.Index < names.Length && !string.IsNullOrWhiteSpace(names[item.Index]) ? names[item.Index] : $"Item {item.Index}" }).ToArray()
        }).ToArray(), dirty, canUndo = undo.Count > 0 };
    }
    object Dex()
    {
        var sav = RequireSave();
        return new { supported = SupportsDex(sav), entries = Enumerable.Range(1, sav.MaxSpeciesID).Where(i=>DexSpeciesAvailable(sav,(ushort)i)).Select(i => new { id = i, name = Species((ushort)i), details = LegacyDex(sav) || sav is SAV8BS or SAV8LA || sav is SAV9SV sv && sv.Personal.IsSpeciesInGame((ushort)i), seen = sav.GetSeen((ushort)i), caught = sav.GetCaught((ushort)i) }).ToArray(), dirty, canUndo = undo.Count > 0 };
    }
}
