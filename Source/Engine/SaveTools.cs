using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    object SpeciesGuide() {
        var sav=RequireSave();var rows=new List<object>();var table=sav.Personal;
        for(ushort species=1;species<=table.MaxSpeciesID;species++) {
            var forms=FormConverter.GetFormList(species,strings.Types,strings.forms,GameInfo.GenderSymbolASCII,sav.Context);
            for(byte form=0;form<table[species].FormCount;form++) {
                if(!table.IsPresentInGame(species,form))continue;
                var p=table.GetFormEntry(species,form);int type1=(int)((MoveType)p.Type1).GetMoveTypeGeneration(sav.Generation),type2=(int)((MoveType)p.Type2).GetMoveTypeGeneration(sav.Generation);
                var native=p switch {PersonalInfo7=>PersonalInfo7.IsPastGenNative(species),PersonalInfo8SWSH x=>x.IsInDex,PersonalInfo8BDSP x=>x.IsInDex,PersonalInfo8LA x=>x.IsPresentInGame,PersonalInfo9SV x=>x.IsInDex,PersonalInfo9ZA x=>x.IsLumioseNative,_=>true};
                rows.Add(new {id=$"{species:D4}-{form:D2}",species,form,name=Species(species),formName=form==0?"":form<forms.Length?forms[form]:$"Form {form}",sprite=SpriteFor(species,form,0,0,sav.Context),native,total=p.BST,catchRate=p.CatchRate,type1,type2,type1Name=MoveLabel(strings.Types,type1),type2Name=MoveLabel(strings.Types,type2),stats=new[]{p.HP,p.ATK,p.DEF,p.SPA,p.SPD,p.SPE},abilities=Enumerable.Range(0,p.AbilityCount).Select(i=>MoveLabel(strings.abilitylist,p.GetAbilityAtIndex(i))).Distinct().ToArray(),alphaMove=p is PersonalInfo9ZA za?MoveLabel(strings.movelist,za.AlphaMove):""});
            }
        }return new {game=GameInfo.GetVersionName(sav.Version),generation=sav.Generation,entries=rows};
    }
    object WorkTarget()=>EventWorkTarget(RequireSave());
    static int WorkCount(object target)=>target switch {IEventWorkArray<byte> x=>x.EventWorkCount,IEventWorkArray<ushort> x=>x.EventWorkCount,IEventWorkArray<int> x=>x.EventWorkCount,IEventWorkArray<uint> x=>x.EventWorkCount,IEventWork<int> x=>x.CountWork,IEventWork<float> x=>x.CountWork,_=>0};
    static object WorkValue(object target,int index)=>target switch {IEventWorkArray<byte> x=>x.GetWork(index),IEventWorkArray<ushort> x=>x.GetWork(index),IEventWorkArray<int> x=>x.GetWork(index),IEventWorkArray<uint> x=>x.GetWork(index),IEventWork<int> x=>x.GetWork(index),IEventWork<float> x=>x.GetWork(index),_=>throw new Exception("Event variables are not available for this game.")};
    object EventWorkRows() {var target=WorkTarget();var count=WorkCount(target);return new {supported=count>0,revision,entries=Enumerable.Range(0,count).Select(i=>new{id=i,value=Convert.ToString(WorkValue(target,i),CultureInfo.InvariantCulture)!,kind=WorkValue(target,i).GetType().Name}).ToArray()};}
    void EditEventWork(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Refresh the event variables.");
        var target=WorkTarget();int id=N(r,"id");if(id<0||id>=WorkCount(target))throw new Exception("Invalid event variable.");var value=S(r,"value");
        SetEventWorkValue(target,id,value);
        if(target is EventWork7 alola)alola.UpdateQrConstants();
        dirty=true;
    }
    ISCBlockArray BlockSave()=>RequireSave() as ISCBlockArray??throw new Exception("This game does not use Switch save blocks.");
    SCBlock RequestedBlock(JsonElement r) {if(!uint.TryParse(S(r,"key"),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var key))throw new Exception("Choose a valid block key.");return BlockSave().AllBlocks.FirstOrDefault(x=>x.Key==key)??throw new Exception("That block is absent from this save.");}
    static bool ScalarBlock(SCBlock b)=>b.Type>=SCTypeCode.Byte && b.Type<=SCTypeCode.Double;
    static string BlockValue(SCBlock b)=>b.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2?(b.Type==SCTypeCode.Bool2?"true":"false"):ScalarBlock(b)?Convert.ToString(b.GetValue(),CultureInfo.InvariantCulture)??"":"";
    object SaveBlockList(JsonElement r) {
        if(save is not ISCBlockArray sav)return new {supported=false,revision,total=0,entries=Array.Empty<object>()};
        var meta=new SCBlockMetadata(sav.Accessor,[]);string q=S(r,"search");int offset=Math.Max(0,N(r,"offset"));
        var all=sav.AllBlocks.Select(b=>new{id=b.Key.ToString("X8"),name=meta.GetBlockName(b,out _)??"Unnamed block",kind=b.Type.ToString(),size=b.Data.Length,value=BlockValue(b)}).Where(x=>q==""||x.id.Contains(q,StringComparison.OrdinalIgnoreCase)||x.name.Contains(q,StringComparison.OrdinalIgnoreCase)).ToArray();
        return new {supported=true,revision,total=all.Length,entries=all.Skip(offset).Take(250).ToArray()};
    }
    object SaveBlockDetail(JsonElement r) {var b=RequestedBlock(r);return new{id=b.Key.ToString("X8"),revision,kind=b.Type.ToString(),size=b.Data.Length,value=BlockValue(b),editable=ScalarBlock(b)||b.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2,hex=Convert.ToHexString(b.Data[..Math.Min(256,b.Data.Length)])};}
    void EditSaveBlock(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Refresh this block.");var b=RequestedBlock(r);string value=S(r,"value");
        if(S(r,"mode")=="import") {if(b.Data.Length==0)throw new Exception("Boolean blocks use the value control.");var path=S(r,"path");if(new FileInfo(path).Length!=b.Data.Length)throw new Exception($"This block requires exactly {b.Data.Length} bytes.");b.ChangeData(File.ReadAllBytes(path));}
        else if(b.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2)b.ChangeBooleanType(bool.Parse(value)?SCTypeCode.Bool2:SCTypeCode.Bool1);
        else if(ScalarBlock(b)) {var converted=Convert.ChangeType(value,b.GetValue().GetType(),CultureInfo.InvariantCulture);if(converted is float f&&!float.IsFinite(f)||converted is double d&&!double.IsFinite(d))throw new Exception("Use a finite number.");b.SetValue(converted);}
        else throw new Exception("Import a binary file to replace this block.");
        dirty=true;
    }
    object ExportSaveBlock(JsonElement r) {
        var b=RequestedBlock(r);var path=ExportPath(S(r,"path"),sourcePath);AtomicWrite(path,b.Data.ToArray());return new {path};
    }
    object ExportBlockArchive(JsonElement r) {
        var sav=BlockSave();using var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)) {
            var metadata=new List<object>();
            foreach(var block in sav.AllBlocks) {
                var entry=zip.CreateEntry($"{block.Key:X8}.bin",CompressionLevel.Fastest);using(var output=entry.Open())output.Write(block.Data);
                metadata.Add(new {key=block.Key.ToString("X8"),type=block.Type.ToString(),subType=block.SubType.ToString(),length=block.Data.Length,value=BlockValue(block)});
            }
            using var manifest=zip.CreateEntry("blocks.json").Open();JsonSerializer.Serialize(manifest,metadata);
        }
        var path=ExportPath(S(r,"path"),sourcePath);AtomicWrite(path,stream.ToArray());return new{path,count=sav.AllBlocks.Count};
    }
}
