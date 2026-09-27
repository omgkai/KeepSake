using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    SaveFile? CloneForUndo() {
        if(demo && save is SAV3 s && s.Data.Length==0) {
            // A synthetic Gen 3 workspace has unpacked buffers but no serialized sectors.
            var copy=BlankSaveFile.Get(s.Version,"Preview");copy.CopyChangesFrom(s);copy.Language=s.Language;return copy;
        }
        if(save is SAV1 or SAV2) {
            // The upstream GB clone repacks lists, normalizing unused party bytes.
            // Preserve both buffers so Undo and rejected actions are byte-exact.
            var field=save.GetType().GetField("Reserved",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The Game Boy save buffer could not be preserved.");
            var reserved=(Memory<byte>)field.GetValue(save)!;
            var raw=save.Data.ToArray();var unpacked=reserved.ToArray();
            var copy=save.Clone();
            raw.CopyTo(save.Data);raw.CopyTo(copy.Data);
            unpacked.CopyTo(((Memory<byte>)field.GetValue(copy)!).Span);
            return copy;
        }
        return save?.Clone();
    }
    static void SetShinyFlag(PKM pk,bool value) {
        // Gen 1/2 shininess is encoded in DVs; the generic non-shiny PID loop cannot change it.
        if(!value && pk is GBPKM gb) {if(gb.IsShiny)gb.IV_DEF=9;return;}
        pk.SetIsShiny(value);
    }
    string JournalKey(PKM? pk) {
        if(pk is null || pk.Species==0)return "";
        var identity=pk.Format>=6?$"ec:{pk.EncryptionConstant}":pk.Format>=3?$"pid:{pk.PID}":$"gb:{pk.Species}:{pk.IV_ATK}:{pk.IV_DEF}:{pk.IV_SPE}:{pk.IV_SPA}";
        var data=JsonSerializer.SerializeToUtf8Bytes(new {demo,identity,pk.ID32,pk.OriginalTrainerName,origin=(int)pk.Version});
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
    }
    string CharacteristicText() => entity is { Format: >= 4 } pk && pk.Characteristic>=0 && pk.Characteristic<strings.characteristics.Length ? strings.characteristics[pk.Characteristic] : "";
    static string ItemIcon(int item,EntityContext context) {
        if(context==EntityContext.Gen8a && HisuiItemIcon(item) is {} supplied)return supplied;
        if(context==EntityContext.Gen9 && PaldeaItemIcon(item) is {} paldea)return paldea;
        var display=ItemConverter.GetItemDisplay(item,context);
        return HeldItemLumpUtil.GetIsLump(display,context) switch {
            HeldItemLumpImage.TechnicalMachine=>"bitem_tm", HeldItemLumpImage.TechnicalRecord=>"bitem_tr", _=>"bitem_"+display
        };
    }
    static bool DexSpeciesAvailable(SaveFile sav,ushort species) => sav switch {
        SAV7b gg=>gg.Personal.IsSpeciesInGame(species),
        SAV9SV sv=>sv.Personal.IsSpeciesInGame(species) && sv.Zukan.GetDexIndex(species) is var index && index.Index!=0 && index.Group<=sv.SaveRevision+1,
        SAV8SWSH sw=>sw.Zukan.GetEntry(species,out _),
        SAV9ZA za=>za.Personal.IsSpeciesInGame(species),
        SAV8LA=>PokedexSave8a.GetDexIndex(PokedexType8a.Hisui,species)!=0,
        _=>true
    };
    static void SetDexFlags(SaveFile sav,ushort species,bool seen,bool caught) {
        if(!DexSpeciesAvailable(sav,species)) throw new Exception("This species is not in this save's Pokédex.");
        if(sav is SAV4 four){four.Dex.SetSeen(species,seen);four.Dex.SetCaught(species,caught);return;}
        if(sav is SAV5 five){if(!seen)five.Zukan.ClearSeen(species);else if(!five.GetSeen(species))five.Zukan.SetSeen(species,five.Personal[species].OnlyFemale?1:0,true);five.Zukan.SetCaught(species,caught);return;}
        if(sav is SAV7 seven){seven.Zukan.SetSeen(species,seen);seven.Zukan.SetCaught(species,caught);return;}
        if(sav is SAV7b letsgo){letsgo.Zukan.SetSeen(species,seen);letsgo.Zukan.SetCaught(species,caught);return;}
        if(sav is SAV8LA la) {
            var dex=la.PokedexSave;
            var forms=Enumerable.Range(0,PersonalTable.LA[species].FormCount).Select(f=>(byte)f).Where(f=>dex.HasFormStorage(species,f)&&!dex.IsBlacklisted(species,f)).ToArray();
            if(forms.Length==0)throw new Exception("This species has no stored Hisui forms.");
            if(!caught)foreach(var f in forms){dex.SetPokeObtainFlags(species,f,0);dex.SetPokeCaughtInWildFlags(species,f,0);}
            if(!seen) {
                foreach(var f in forms)dex.SetPokeSeenInWildFlags(species,f,0);
                // Same typed research entry used by PKHeX; counters and reported research remain intact.
                new PokedexSaveData(la.Blocks.GetBlock(0x02168706).Raw).GetResearchEntry(species).HasEverBeenUpdated=false;
            } else {
                dex.SetPokeHasBeenUpdated(species);
                if(caught&&!la.GetCaught(species))dex.SetPokeObtainFlags(species,forms[0],(byte)(PersonalTable.LA[species].OnlyFemale?2:1));
            }
            return;
        }
        if(sav is SAV8SWSH sw) {
            sw.Zukan.SetCaught(species,caught);
            if(!seen)for(byte f=0;f<64;f++)for(int region=0;region<4;region++)sw.Zukan.SetSeenRegion(species,f,region,false);
            else if(!sw.Zukan.GetSeen(species))sw.Zukan.SetSeenRegion(species,0,sw.Personal[species].OnlyFemale?1:0,true);
            return;
        }
        if(sav is SAV9ZA za) {
            var zaEntry=za.Zukan.GetEntry(species);
            if(!caught)for(byte f=0;f<32;f++)zaEntry.SetIsFormCaught(f,false);
            if(!seen)for(byte f=0;f<32;f++)zaEntry.SetIsFormSeen(f,false);
            if(seen&&!zaEntry.IsSeen)zaEntry.SetIsFormSeen(0,true);
            if(caught&&!zaEntry.IsCaught)zaEntry.SetIsFormCaught(0,true);
            return;
        }
        if(sav is SAV8BS bd) {bd.Zukan.SetState(species,(ZukanState8b)(caught?3:seen?2:0));return;}
        if(sav is not SAV9SV sv) {sav.SetSeen(species,seen);sav.SetCaught(species,caught);return;}
        var z=sv.Zukan;
        if(z.GetRevision()==0) {z.DexPaldea.Get(species).SetState((uint)(caught?3:seen?2:0));return;}
        var entry=z.DexKitakami.Get(species);
        if(!caught) for(byte f=0;f<32;f++)entry.SetObtainedForm(f,false);
        if(!seen) for(byte f=0;f<32;f++)entry.SetSeenForm(f,false);
        if(seen && !z.GetSeen(species) || caught && !z.GetCaught(species)) {
            byte form=0;while(form<31 && !sv.Personal.GetFormEntry(species,form).IsPresentInGame)form++;
            if(seen)entry.SetSeenForm(form,true);
            if(caught)entry.SetObtainedForm(form,true);
        }
    }
    void GiveDex() {
        var sav=RequireSave();if(!SupportsDex(sav))throw new Exception("This save has no supported Pokédex flags.");
        for(ushort i=1;i<=sav.MaxSpeciesID;i++)if(DexSpeciesAvailable(sav,i))SetDexFlags(sav,i,true,true);
        dirty=true;
    }
    void GiveInventory(JsonElement r) {
        var sav=RequireSave();var bag=sav.Inventory;var index=N(r,"pouch");
        if(index<0 || index>=bag.Pouches.Count)throw new Exception("Choose a pouch first.");
        var pouch=bag.Pouches[index];
        foreach(var item in bag.Info.GetItems(pouch.Type)) {
            if(item==0 || !bag.IsLegal(pouch.Type,item,1))continue;
            if(pouch.GiveItem(bag,item)<0)throw new Exception("This pouch cannot fit every item. Nothing was changed; use Add Items to choose a smaller selection.");
        }
        bag.CopyTo(sav);dirty=true;
    }
    object SlotPreview(JsonElement r) {
        var sav=RequireSave();int b=N(r,"box"),s=N(r,"slot");bool isParty=B(r,"party");
        if(N(r,"revision")!=revision)throw new Exception("The slot changed. Hover again to refresh.");
        if(isParty ? s<0 || s>=sav.PartyCount : b<0 || b>=sav.BoxCount || s<0 || s>=sav.BoxSlotCount)throw new Exception("Invalid slot.");
        var pk=isParty?sav.GetPartySlotAtIndex(s):sav.GetBoxSlotAtIndex(b,s);
        var analysis=settings.PKHaXMode?null:new LegalityAnalysis(pk);Span<ushort> moveIDs=stackalloc ushort[4];pk.GetMoves(moveIDs);
        var moves=moveIDs.ToArray().Select((m,i)=> {
            byte type=MoveInfo.GetType(m,pk.Context);var name=MoveLabel(strings.movelist,m);
            if(m==(ushort)Move.HiddenPower && pk.Context!=EntityContext.Gen8a && HiddenPower.TryGetTypeIndex(pk.HPType,out type))name+=$" ({MoveLabel(strings.Types,type)}) [{pk.HPPower}]";
            return new {name,type,typeName=MoveLabel(strings.Types,type),legal=analysis?.Parsed==true&&analysis.Info.Moves[i].Valid};
        }).ToArray();
        var order=BattleTemplateConfig.CommunityStandard.ToArray().Where(t=>t is not (BattleTemplateToken.FirstLine or BattleTemplateToken.Moves)).ToArray();
        var config=new BattleTemplateExportSettings(order,"en");
        var encounterLines=new List<string>();if(analysis!=null)LegalityFormatting.AddEncounterInfo(LegalityLocalizationContext.Create(analysis),encounterLines);
        var previewText=ShowdownParsing.GetLocalizedPreviewText(pk,config);
        if(pk is IGanbaru grit) {Span<byte> values=stackalloc byte[6];grit.GetGVs(values);previewText+="\nGrit (HP/Atk/Def/Spe/SpA/SpD): "+string.Join(" / ",values.ToArray());}
        if(pk is IAwakened awakened) {Span<byte> values=stackalloc byte[6];awakened.GetAVs(values);previewText+="\nAVs (HP/Atk/Def/Spe/SpA/SpD): "+string.Join(" / ",values.ToArray());}
        return new {name=pk.Nickname,species=Species(pk.Species),sprite=Sprite(pk),portrait=Portrait(pk),gender=pk.Gender,ball=pk.Ball,item=ItemIcon(pk.HeldItem,pk.Context),itemName=MoveLabel(strings.GetItemStrings(pk.Context,pk.Version),pk.HeldItem),shiny=pk.IsShiny,egg=pk.IsEgg,alpha=pk is IAlphaReadOnly {IsAlpha:true},level=pk.CurrentLevel,
            text=previewText,encounter=string.Join("\n",encounterLines),moves,uncheckedLegality=settings.PKHaXMode,legal=analysis?.Valid==true,report=analysis?.Report()??"PKHaX mode: automatic legality checks are disabled.",origin=GameInfo.GetVersionName(pk.Version),trainer=pk.OriginalTrainerName};
    }
    bool FashionSupported()=>save is SAV6XY or SAV7SM or SAV7USUM or SAV7b or SAV8SWSH or SAV8BS or SAV8LA or SAV9SV or SAV9ZA;
    object? FashionObject()=>save switch {SAV9SV s=>s.PlayerFashion,SAV9ZA s=>s.PlayerFashion,SAV8LA s=>s.Blocks.FashionPlayer,_=>null};
    bool CanUnlockFashion()=>FashionSupported();
    object Fashion()=>new {supported=FashionSupported(),canUnlock=CanUnlockFashion(),revision,items=save is {} current?WardrobeItems(current):[],description=save switch {
        SAV9SV=>"Browse the PKHeX clothing catalog for this trainer. Give All includes the entries supported by the bundled core, including its DLC clothing IDs.",
        SAV7SM or SAV7USUM=>"Give All replaces the wardrobe unlock flags with PKHeX's full legal wardrobe for this trainer. Undo restores the previous wardrobe.",
        SAV9ZA=>"Give All marks existing wardrobe entries as owned. It keeps equipped and other flags intact.",
        SAV8LA=>"Choose individual Hisui garments or Give All. Named catalog entries are supported; unused entries and other progress are preserved.",
        _=>"Unlock this game's wardrobe with PKHeX's fashion tools. Undo restores the previous wardrobe; Export Copy saves the change."
    },fields=FashionObject() is {} obj?Fields(obj,"Equipped outfit").Where(f=>f.editable && !f.id.StartsWith('_')).ToArray():Array.Empty<Field>()};
    void EditFashion(JsonElement r) {
        var obj=FashionObject()??throw new Exception("Equipped outfit editing is unavailable for this save.");
        var id=S(r,"field");if(!Fields(obj,"Outfit").Any(f=>f.id==id&&f.editable&&!id.StartsWith('_')))throw new Exception("Choose an editable outfit field.");
        SetProperty(obj,id,S(r,"value"));dirty=true;
    }
    void UnlockFashion() {
        switch(RequireSave()) {
            case SAV8LA s:foreach(var row in WardrobeItems(s).Where(x=>x.editable&&!x.owned))SetWardrobeItem(row,true);break;
            case SAV6XY s:s.Blocks.Fashion.UnlockAllAccessories();break;
            case SAV7 s when s is SAV7SM or SAV7USUM:
                var name=$"fashion_{(s.Gender==0?"m":"f")}_{(s is SAV7USUM?"uu":"sm")}";
                var data=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fashion",name));s.Fashion.Clear();s.Fashion.ImportPayload(data);break;
            case SAV7b s:s.Blocks.FashionPlayer.UnlockAllAccessoriesPlayer();s.Blocks.FashionStarter.UnlockAllAccessoriesStarter();break;
            case SAV8SWSH s:s.Fashion.UnlockAllLegal();break;
            case SAV8BS s:new EventUnlocker8b(s).UnlockFashion();break;
            case SAV9SV s:PlayerFashionUnlock9.UnlockBase(s.Blocks,s.Gender);break;
            case SAV9ZA s:
                uint[] keys=[SaveBlockAccessor9ZA.KFashionTops,SaveBlockAccessor9ZA.KFashionBottoms,SaveBlockAccessor9ZA.KFashionAllInOne,SaveBlockAccessor9ZA.KFashionHeadwear,SaveBlockAccessor9ZA.KFashionEyewear,SaveBlockAccessor9ZA.KFashionGloves,SaveBlockAccessor9ZA.KFashionLegwear,SaveBlockAccessor9ZA.KFashionFootwear,SaveBlockAccessor9ZA.KFashionSatchels,SaveBlockAccessor9ZA.KFashionEarrings];
                foreach(var key in keys)FashionItem9a.ModifyAll(s.Blocks.GetBlock(key).Data,x=>x.IsOwned=true);break;
            default:throw new Exception("This save has no supported wardrobe unlock action.");
        }
        dirty=true;
    }
}
