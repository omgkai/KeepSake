using System.Text;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    object MoveRecords()
    {
        var pk = RequireEntity();
        var shop = pk as IMoveShop8Mastery;
        var record = pk as ITechRecord;
        var permit = shop?.Permit ?? record?.Permit ?? throw new Exception("This format has no move records.");
        var indexes = permit.RecordPermitIndexes.ToArray();
        return new {
            shop = shop != null,
            entries = indexes.Select((move,i) => new {
                id=i, move, name=strings.movelist[move], type=strings.Types[MoveInfo.GetType(move,pk.Context)],
                permitted=permit.IsRecordPermitted(i),
                learned=shop?.GetPurchasedRecordFlag(i) ?? record!.GetMoveRecordFlag(i),
                mastered=shop?.GetMasteredRecordFlag(i) ?? false
            }).ToArray()
        };
    }
    void EditMoveRecords(JsonElement r)
    {
        var pk = RequireEntity();
        var shop = pk as IMoveShop8Mastery; var record = pk as ITechRecord;
        var permit = shop?.Permit ?? record?.Permit ?? throw new Exception("This format has no move records.");
        var mode = S(r,"mode","one");
        if (mode == "one")
        {
            int index=N(r,"index");
            if (index < 0 || index >= permit.RecordPermitIndexes.Length) throw new Exception("Invalid move record.");
            if (shop != null) { shop.SetPurchasedRecordFlag(index,B(r,"learned")); shop.SetMasteredRecordFlag(index,B(r,"mastered")); }
            else record!.SetMoveRecordFlag(index,B(r,"learned"));
        }
        else if (shop != null)
        {
            switch (mode) {
                case "clear": shop.ClearMoveShopFlags(); break;
                case "current": shop.SetMoveShopFlags(pk); break;
                case "all": shop.SetPurchasedFlagsAll(pk); shop.SetMoveShopFlagsAll(pk); break;
                default: throw new Exception("Unknown move-shop action.");
            }
        }
        else
        {
            var option=mode switch {
                "clear" => TechnicalRecordApplicatorOption.None,
                "current" => TechnicalRecordApplicatorOption.LegalCurrent,
                "all" => TechnicalRecordApplicatorOption.LegalAll,
                "force" => TechnicalRecordApplicatorOption.ForceAll,
                _ => throw new Exception("Unknown record action.")
            };
            record!.SetRecordFlags(pk,option);
        }
        pk.RefreshChecksum(); pending=true;
    }
    static readonly (PokedexResearchTaskType8a type,int index)[] researchCounters = BuildResearchCounters();
    static (PokedexResearchTaskType8a,int)[] BuildResearchCounters()
    {
        var names = new[] { "Catch","CatchAlpha","CatchLarge","CatchSmall","CatchHeavy","CatchLight","CatchAtTime","CatchSleeping","CatchInAir","CatchNotSpotted","UseMove","UseMove","UseMove","UseMove","DefeatWithMoveType","DefeatWithMoveType","DefeatWithMoveType","Defeat","UseStrongStyleMove","UseAgileStyleMove","Evolve","GiveFood","StunWithItems","ScareWithScatterBang","LureWithPokeshiDoll","LeapFromTrees","LeapFromLeaves","LeapFromSnow","LeapFromOre","LeapFromTussocks" };
        return names.Select((s,i)=>(Enum.Parse<PokedexResearchTaskType8a>(s),i is >=10 and <=13 ? i-10 : i is >=14 and <=16 ? i-14 : -1)).ToArray();
    }
    static ushort ResearchSpecies(int species)
    {
        if (species < 1 || species > PersonalTable.LA.MaxSpeciesID || PokedexSave8a.GetDexIndex(PokedexType8a.Hisui,(ushort)species)==0) throw new Exception("Choose a species from the Hisui Pokédex.");
        return (ushort)species;
    }
    object Research(int id)
    {
        var sav=RequireSave() as SAV8LA ?? throw new Exception("Research tasks are specific to Legends: Arceus.");
        ushort species=ResearchSpecies(id); var dex=sav.PokedexSave;
        int dexIndex=PokedexSave8a.GetDexIndex(PokedexType8a.Hisui,species);
        var definitions=PokedexConstants8a.ResearchTasks[dexIndex-1];
        var descriptions=Util.GetStringList("tasks8a", settings.CatalogLanguage);
        var timeDescriptions=Util.GetStringList("time_tasks8a", settings.CatalogLanguage);
        var quests=Util.GetStringList("species_tasks8a", settings.CatalogLanguage);
        var tasks = new List<object>();
        int unreportedPoints = 0;
        object TaskRow(int id, PokedexResearchTask8a? definition, int definitionIndex, int count, string fallback)
        {
            int reported=0, points=0;
            if (definition != null)
            {
                int unreported, level;
                try { unreported=dex.GetResearchTaskLevel(species,definitionIndex,out level,out count,out _); }
                catch (ArgumentOutOfRangeException) when (demo && definition.Task is PokedexResearchTaskType8a.PartOfArceus or PokedexResearchTaskType8a.SpeciesQuest) { unreported=0; level=1; count=0; }
                reported=Math.Max(0,level-1);
                points=definition.PointsSingle+definition.PointsBonus;
                unreportedPoints+=unreported*points;
            }
            return new { id, name=definition?.GetTaskLabelString(descriptions,timeDescriptions,quests) ?? fallback, count,
                active=definition!=null, thresholds=definition?.TaskThresholds.Select(v=>(int)v).ToArray() ?? [],
                required=definition?.RequiredForCompletion ?? false, reported, points,
                bonus=definition?.PointsBonus > 0, editable=definition?.Task.CanSetCurrentValue() ?? true };
        }
        for (int i=0;i<researchCounters.Length;i++)
        {
            var counter=researchCounters[i];
            dex.GetResearchTaskProgressByForce(species,counter.type,counter.index,out int count);
            int index=Array.FindIndex(definitions,t=>t.Task==counter.type && t.Index==counter.index);
            tasks.Add(TaskRow(i,index<0?null:definitions[index],index,count,Label(counter.type.ToString())));
        }
        for (int i=0;i<definitions.Length;i++)
            if (!researchCounters.Any(c=>c.type==definitions[i].Task && c.index==definitions[i].Index))
                tasks.Add(TaskRow(30+i,definitions[i],i,0, ""));
        var choices=Enumerable.Range(1,PersonalTable.LA.MaxSpeciesID).Where(i=>PokedexSave8a.GetDexIndex(PokedexType8a.Hisui,(ushort)i)!=0).Select(i=>new Choice(i.ToString(),Species((ushort)i))).ToArray();
        return new {species=id,name=Species(species),tasks,choices,unreportedPoints,points=dex.GetPokeResearchRate(species),perfect=dex.IsPerfect(species),solitude=dex.GetSolitudeComplete(species)};
    }
    void EditResearch(JsonElement r)
    {
        var sav=RequireSave() as SAV8LA ?? throw new Exception("Research tasks are specific to Legends: Arceus.");
        var species=ResearchSpecies(N(r,"species")); var dex=sav.PokedexSave;
        switch (S(r,"mode","counts"))
        {
            case "counts":
                foreach(var entry in r.GetProperty("edits").EnumerateArray())
                {
                    int id=N(entry,"id"), count=N(entry,"count");
                    if (id<0 || id>=researchCounters.Length || count<0 || count>60000) throw new Exception("Research counts must be between 0 and 60,000.");
                    var counter=researchCounters[id]; dex.SetResearchTaskProgressByForce(species,counter.type,count,counter.index);
                }
                break;
            case "report": dex.UpdateSpecificReportPoke(species); break;
            case "solitude": dex.SetSolitudeComplete(species,B(r,"value")); break;
            default: throw new Exception("Unknown research action.");
        }
        dirty=true;
    }
    record StorageRow(string id,int box,int slot,bool party,string name,string nickname,int species,int level,bool shiny,bool alpha,string nature,string ability,string item,string trainer,string moves,string sprite);
    IEnumerable<(PKM pk,int b,int s,bool party)> StoredPokemon()
    {
        var sav=RequireSave();
        for(int b=0;b<sav.BoxCount;b++) for(int s=0;s<sav.BoxSlotCount && b*sav.BoxSlotCount+s<sav.SlotCount;s++) {
            var pk=sav.GetBoxSlotAtIndex(b,s); if(pk.Species!=0) yield return (pk,b,s,false);
        }
        if(sav.HasParty) for(int s=0;s<sav.PartyCount;s++) {var pk=sav.GetPartySlotAtIndex(s); if(pk.Species!=0) yield return(pk,0,s,true);}
    }
    StorageRow[] StorageRows() => StoredPokemon().Select(x=> {
        var pk=x.pk;
        string NameAt(string[] names,int id)=>id>=0 && id<names.Length && !string.IsNullOrWhiteSpace(names[id]) ? names[id] : id.ToString();
        var moves=new[]{pk.Move1,pk.Move2,pk.Move3,pk.Move4}.Where(m=>m!=0).Select(m=>NameAt(strings.movelist,m));
        return new StorageRow($"{x.party}:{x.b}:{x.s}",x.b,x.s,x.party,Species(pk.Species),pk.Nickname,pk.Species,pk.CurrentLevel,pk.IsShiny,pk is IAlphaReadOnly {IsAlpha:true},pk.Nature.ToString(),NameAt(strings.abilitylist,pk.Ability),NameAt(strings.GetItemStrings(pk.Context,pk.Version),pk.HeldItem),pk.OriginalTrainerName,string.Join(" / ",moves),Sprite(pk));
    }).ToArray();
    object ExportBoxes(JsonElement r)
    {
        if(pending) throw new Exception("Set your Pokémon edits to a slot before exporting boxes.");
        string parent=Path.GetFullPath(S(r,"path"));
        if(!Directory.Exists(parent)) throw new Exception("Choose an existing folder.");
        string dest=Path.Combine(parent,"PKHeX Boxes " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + " " + Guid.NewGuid().ToString("N")[..6]);
        var sav=RequireSave().Clone(); sav.CurrentBox=box;
        var settings=this.settings.BoxExport with {
            Scope=B(r,"all") ? BoxExportScope.All : BoxExportScope.Current,
            EmptySlots=B(r,"empty") ? BoxExportEmptySlots.Include : BoxExportEmptySlots.Skip
        };
        Directory.CreateDirectory(dest);
        try { int count=BoxExport.Export(sav,dest,settings); return new {count,path=dest}; }
        catch { Directory.Delete(dest,true); throw; }
    }
    static readonly string[] reportDefault=["Location","Slot","Species","Nickname","Level","Shiny","Alpha","Nature","Ability","Item","Trainer","Moves"];
    Choice[] ReportColumns()=>reportDefault.Select(x=>new Choice(x,x)).Concat(EntityFields(RequireSave().BlankPKM).Select(f=>new Choice("pk."+f.id,f.label+" ("+f.id+")"))).DistinctBy(x=>x.value).ToArray();
    record ReportRow(string id,string[] values);
    record ReportPage(Choice[] columns,ReportRow[] rows);
    ReportPage StorageReportData(JsonElement r)
    {
        var available=ReportColumns();var allowed=available.ToDictionary(x=>x.value,x=>x.label);
        var columns=r.TryGetProperty("columns",out var selected) ? selected.EnumerateArray().Select(x=>x.GetString()!).ToArray():reportDefault;
        if(columns.Length is <1 or >64 || columns.Distinct().Count()!=columns.Length || columns.Any(x=>!allowed.ContainsKey(x)))throw new Exception("Choose 1–64 distinct report columns from the available list.");
        var rows=StorageRows();
        if(r.TryGetProperty("ids",out var selectedIDs)){var ids=selectedIDs.EnumerateArray().Select(x=>x.GetString()!).ToHashSet();if(ids.Any(id=>!rows.Any(p=>p.id==id)))throw new Exception("The storage selection changed. Refresh the report.");rows=rows.Where(x=>ids.Contains(x.id)).ToArray();}
        var raw=StoredPokemon().ToDictionary(x=>$"{x.party}:{x.b}:{x.s}",x=>x.pk);
        var result=new List<ReportRow>();
        foreach(var p in rows) {
            var values=new Dictionary<string,string>{{"Location",p.party?"Party":$"Box {p.box+1}"},{"Slot",(p.slot+1).ToString()},{"Species",p.name},{"Nickname",p.nickname},{"Level",p.level.ToString()},{"Shiny",p.shiny.ToString()},{"Alpha",p.alpha.ToString()},{"Nature",p.nature},{"Ability",p.ability},{"Item",p.item},{"Trainer",p.trainer},{"Moves",p.moves}};
            if(columns.Any(x=>x.StartsWith("pk.")))foreach(var field in EntityFields(raw[p.id]))values["pk."+field.id]=field.value;
            result.Add(new(p.id,columns.Select(c=>values.GetValueOrDefault(c,"")).ToArray()));
        }
        return new(columns.Select(c=>new Choice(c,allowed[c])).ToArray(),result.ToArray());
    }
    object ExportStorageReport(JsonElement r)
    {
        if(pending)throw new Exception("Set your Pokémon edits to a slot before exporting a report.");
        var path=ExportPath(S(r,"path"),sourcePath);var report=StorageReportData(r);
        string Cell(string s){if(s.TrimStart().StartsWith('=')||s.TrimStart().StartsWith('+')||s.TrimStart().StartsWith('-')||s.TrimStart().StartsWith('@')||s.StartsWith('\t')||s.StartsWith('\r'))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
        var lines=new[]{string.Join(",",report.columns.Select(c=>Cell(c.label)))}.Concat(report.rows.Select(row=>string.Join(",",row.values.Select(Cell))));
        AtomicWrite(path,Encoding.UTF8.GetBytes(string.Join("\r\n",lines)+"\r\n"));
        return new {count=report.rows.Length,path};
    }
}
