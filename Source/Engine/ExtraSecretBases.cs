using System.Reflection;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    SecretBaseManager3 Bases3=>((ISaveBlock3LargeHoenn)((SAV3)RequireSave()).LargeBlock).SecretBases;
    SecretBase6Block Bases6=>((SAV6AO)RequireSave()).SecretBase;
    SecretBase6 Base6(int i)=>i==0?Bases6.GetSecretBaseSelf():Bases6.GetSecretBaseOther(i-1);
    ExtraValue[] BaseFields(object obj,int generation) {
        var fields=new List<ExtraValue>();
        foreach(var p in obj.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.CanWrite&&p.GetIndexParameters().Length==0)) {
            string id=p.Name;if(id is "SpeciesInternal" or "SpriteItem" or "Unused11" or "Checksum" or "Sanity" or "IsEgg")continue;
            var v=p.GetValue(obj);if(v is string str){fields.Add(ET(id,Label(id),str,generation==3?7:id=="TrainerName"?12:16) with {group=id.StartsWith("Say")||id.StartsWith("Team")?"Messages":"Trainer"});continue;}
            if(v is not (byte or ushort or uint or int or bool or Enum))continue;
            decimal max=v is byte?255:v is ushort?65535:v is uint?uint.MaxValue:int.MaxValue,min=0;
            max=id switch {"BaseLocation"=>85,"SecretBaseLocation"=>255,"RegistryStatus"=>3,"AbilityNumber"=>7,"Form"=>31,"Ability"=>191,"Gender" or "OriginalTrainerGender"=>2,"HeldItem"=>generation==3?376:775,"Good"=>172,"Rotation"=>3,"Level" or "CurrentLevel"=>100,"BoppoyamaScore" or "CurrentFriendship" or "TimesEntered" or "EVAll"=>255,_=>id.StartsWith("IV_")?31:id.StartsWith("EV_")?252:id.EndsWith("_PPUps")?3:max};
            if(id=="BaseLocation")min=-1;
            Choice[]? choices=id switch {
                "Species"=>Enumerable.Range(0,generation==3?387:722).Select(n=>new Choice(n.ToString(),Species((ushort)n))).ToArray(),
                "Gender" when obj is SecretBase6PKM=>[new("0","Male ♂"),new("1","Female ♀"),new("2","Genderless")],
                "Gender" or "OriginalTrainerGender"=>[new("0","Boy ♂"),new("1","Girl ♀")],
                "Nature"=>Enumerable.Range(0,25).Select(n=>new Choice(n.ToString(),strings.natures[n])).ToArray(),
                "Rank"=>ExtraEnums<SecretBase6Rank>(),
                "Language"=>GameInfo.LanguageDataSource((byte)generation,generation==3?EntityContext.Gen3:EntityContext.Gen6).Select(x=>new Choice(x.Value.ToString(),x.Text)).ToArray(),
                "BaseLocation"=>Enumerable.Range(-1,87).Where(n=>n is not (1 or 2)).Select(n=>new Choice(n.ToString(),n<=0?(n==-1?"Not created":"Empty"):$"Location {n}")).ToArray(),
                "Ability"=>Enumerable.Range(0,192).Select(n=>new Choice(n.ToString(),strings.abilitylist[n])).ToArray(),
                "HeldItem"=>Enumerable.Range(0,generation==3?377:776).Select(n=>new Choice(n.ToString(),generation==3?GameInfo.Strings.GetItemStrings(EntityContext.Gen3)[n]:strings.itemlist[n])).ToArray(),
                _=>id.Length==5&&id.StartsWith("Move")?Enumerable.Range(0,generation==3?355:622).Select(n=>new Choice(n.ToString(),strings.movelist[n])).ToArray():null
            };
            fields.Add(EV(id,Label(id),v,max,min,choices) with {group=id.StartsWith("IV_")||id.StartsWith("EV_")?"Stats":id.StartsWith("Move")?"Moves":id is "PID" or "EncryptionConstant" or "Param1" or "Param2"?"Advanced":"Details"});
        }return fields.ToArray();
    }
    ExtraPage ReadBases(ExtraTool tool,string? detailId) {
        var rows=new List<ExtraRow>();bool gen3=tool.id=="bases3";int count=gen3?Bases3.Count:31;
        if(!gen3) {
            rows.Add(ER("settings","Your base settings",[EP(Bases6,"SecretBaseSelfLocation","Map location",65535),EP(Bases6,"SecretBaseHasFlag","Flag available")],"Your base's map location and flag"));
            for(int i=0;i<SecretBase6Block.Count_Goods_Used;i++){var g=Bases6.GetGood(i);rows.Add(ER("good:"+i,$"Decoration {i+1}",[EP(g,"Count","Owned",25),EP(g,"IsNew","New")],"Decoration stock"));}
        }
        for(int i=0;i<count;i++) {
            string prefix=i.ToString(),id="base:"+i;object b=gen3?Bases3.Bases[i]:Base6(i);string name=gen3?((SecretBase3)b).OriginalTrainerName:((SecretBase6)b).TrainerName;
            rows.Add(ER(id,(i==0&&!gen3?"Your base":$"Base {i+1}")+" · "+(string.IsNullOrWhiteSpace(name)?"Empty":name),detailId==id?BaseFields(b,gen3?3:6):[],gen3?((SecretBase3)b).OriginalTrainerClassName:$"Location {((SecretBase6)b).BaseLocation}",actions:!gen3&&i>0?[new("delete","Delete Base")]:[]) with {fileExtension=gen3?null:"sb6"});
            for(int k=0;k<(gen3?6:i==0?0:3);k++) {
                string pid=$"{prefix}:pokemon:{k}";object pk=gen3?((SecretBase3)b).Team.Team[k]:((SecretBase6Other)b).GetParticipant(k);ushort species=gen3?((SecretBase3PKM)pk).Species:((SecretBase6PKM)pk).Species;
                rows.Add(ER(pid,$"Pokémon {k+1} · {Species(species)}",detailId==pid?BaseFields(pk,gen3?3:6):[],"Base battle team",gen3?SpriteFor(species,((SecretBase3PKM)pk).Form,((SecretBase3PKM)pk).Gender,0,save!.Context):SpriteFor(species,((SecretBase6PKM)pk).Form,((SecretBase6PKM)pk).Gender,0,save!.Context,((SecretBase6PKM)pk).IsShiny)));
            }
            for(int k=0;k<(gen3?16:28);k++) {
                string did=$"{prefix}:placement:{k}";ExtraValue[] fs=[];
                if(detailId==did)fs=gen3?[EV("Decoration","Decoration ID",((SecretBase3)b).GetDecorations()[k],255),EV("Coordinate","Stored coordinate",((SecretBase3)b).GetDecorationCoordinates()[k],255)]:BaseFields(((SecretBase6)b).GetPlacement(k),6);
                rows.Add(ER(did,$"Placement {k+1}",fs,"Decoration and position"));
            }
        }
        return new(tool.id,revision,tool,rows.ToArray(),gen3?[]:[new("goods","Give All Decorations"),new("cleargoods","Clear Decoration Stock")],!gen3);
    }
    void EditBases(JsonElement request,string kind,string id,string mode,Dictionary<string,string> edits) {
        bool gen3=kind=="bases3";
        if(mode is "goods" or "cleargoods"){if(mode=="goods")Bases6.GiveAllGoods();else for(int g=0;g<SecretBase6Block.Count_Goods_Used;g++)Bases6.GetGood(g).Clear();return;}
        var parts=id.Split(':');
        if(id=="settings"){SetExtraProperties(Bases6,edits);return;}
        if(parts[0]=="good"){SetExtraProperties(Bases6.GetGood(int.Parse(parts[1])),edits);return;}
        int i=int.Parse(parts[0]=="base"?parts[1]:parts[0]);
        if(gen3){var manager=Bases3;var b=manager.Bases[i];if(parts[0]=="base"){if(edits.Remove("Language",out var language))b.Language=int.Parse(language);SetExtraProperties(b,edits);}else{int k=int.Parse(parts[2]);if(parts[1]=="pokemon"){var team=b.Team;SetExtraProperties(team.Team[k],edits);b.Team=team;}else foreach(var (key,v) in edits){if(key=="Decoration")b.GetDecorations()[k]=byte.Parse(v);else b.GetDecorationCoordinates()[k]=byte.Parse(v);}}manager.Save();return;}
        var base6=Base6(i);
        if(mode=="delete"){if(i==0)throw new Exception("Your own base cannot be deleted here.");Bases6.DeleteOther(i-1);return;}
        if(mode=="import"){if(parts[0]!="base")throw new Exception("Select a base profile to import.");string path=S(request,"path");long size=new FileInfo(path).Length;if(size is not (SecretBase6.SIZE or SecretBase6Other.SIZE))throw new Exception("Choose a 784-byte or 992-byte secret-base file.");base6.Load(SecretBase6.Read(ReadExtraBytes(path,(int)size))!);return;}
        if(parts[0]=="base")SetExtraProperties(base6,edits);
        else if(parts[1]=="pokemon"){var other=(SecretBase6Other)base6;int k=int.Parse(parts[2]);var pk=other.GetParticipant(k);SetExtraProperties(pk,edits);other.SetParticipant(k,pk);}
        else SetExtraProperties(base6.GetPlacement(int.Parse(parts[2])),edits);
    }
    byte[] ExportBase(string id){var parts=id.Split(':');if(parts.Length!=2||parts[0]!="base"||!int.TryParse(parts[1],out int i)||i<0||i>30)throw new Exception("Select a base profile.");return Base6(i).Data.ToArray();}
}
