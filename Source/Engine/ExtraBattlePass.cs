using PKHeX.Core;
using System.Reflection;
using System.Text.Json;

sealed partial class EditorSession {
    static readonly Dictionary<string,GearCategory> PassGear=new(){["Head"]=GearCategory.Head,["Hair"]=GearCategory.Hair,["Face"]=GearCategory.Face,["Glasses"]=GearCategory.Glasses,["Top"]=GearCategory.Top,["Hands"]=GearCategory.Hands,["Bottom"]=GearCategory.Bottom,["Shoes"]=GearCategory.Shoes,["Badge"]=GearCategory.Badges,["Bag"]=GearCategory.Bags};
    static readonly Dictionary<string,int> PassTextLimits=new(){["Name"]=11,["CreatorName"]=11,["BirthMonth"]=4,["BirthDay"]=4,["Greeting"]=25,["SentOut"]=27,["Shift1"]=25,["Shift2"]=25,["Win"]=51,["Lose"]=51,["SelfIntroduction"]=53,["RegionCode"]=4};
    static readonly HashSet<string> PassMessages=["Greeting","SentOut","Shift1","Shift2","Win","Lose","SelfIntroduction"];
    ExtraValue[] PassFields(BattlePass pass) {
        var fields=new List<ExtraValue>();var gear=GameLanguage.GetStrings("gear","en");var designs=GameLanguage.GetStrings("pass_design","en");var title1=GameLanguage.GetStrings("trainer_title","en");var title2=GameLanguage.GetStrings("trainer_title_npc","en");
        foreach(var p in typeof(BattlePass).GetProperties().Where(p=>p.CanRead&&p.CanWrite&&!p.Name.StartsWith("Unknown"))) {
            string key=p.Name;var value=p.GetValue(pass);string group=PassMessages.Contains(key)||key.StartsWith("Preset")?"Messages":PassGear.ContainsKey(key)||key is "Model" or "Skin" or "PictureType" or "PassDesign"?"Appearance":key.StartsWith("Record")||key=="Battles"?"Records":"Trainer";
            if(value is string str){int limit=PassTextLimits[key];if(key=="RegionCode")str=str.TrimEnd('\0');if(key=="SelfIntroduction"){str=str.TrimStart(StringConverter4GC.Proportional);limit=pass.Language==BattlePassLanguage.Japanese?53:51;}bool message=PassMessages.Contains(key);if(message)str=str.Replace(StringConverter4GC.LineBreak,'\n');fields.Add(new(key,Label(key),str,message?"multiline":"text","0",limit.ToString(),[],group));continue;}
            if(key=="PlayerID"){fields.Add(new(key,"Player ID",pass.PlayerID.ToString("X16"),"hex","0","16",[],"Trainer"));continue;}
            if(value is not (bool or byte or ushort or short or int or Enum))continue;
            decimal max=value is bool?1:value is ushort?65535:value is short?32767:value is byte?255:int.MaxValue;
            if(key.EndsWith("Clears"))max=255;
            Choice[]? choices=key switch {
                "Model"=>ExtraEnums<ModelBR>(),"Language"=>ExtraEnums<BattlePassLanguage>(),"Skin"=>ExtraEnums<SkinColorBR>(),"PictureType"=>[new("0","Full body"),new("1","Portrait")],
                "PassDesign"=>designs.Select((s,i)=>new Choice(i.ToString(),s)).ToArray(),
                "TrainerTitle"=>new[]{new Choice("0","None")}.Concat(title1.Select((s,i)=>new Choice((11057+i).ToString(),$"{s} · {Label(((ModelBR)(i/(title1.Length/6)+1)).ToString())}"))).Concat(title2.Select((s,i)=>new Choice((18015+i).ToString(),s+" · NPC"))).ToArray(),
                "Country"=>Util.GetCountryRegionList("gen4_countries","en").Select(c=>new Choice(c.Value.ToString(),c.Text)).ToArray(),
                _=>null
            };
            if(key=="Country"||key=="Region")max=65535;
            if(PassGear.TryGetValue(key,out var category)){var model=(ModelBR)pass.Model;if(model is >=ModelBR.YoungBoy and <=ModelBR.LittleGirl){if(category==GearCategory.Badges)model=ModelBR.YoungBoy;var (offset,count)=GearUnlock.GetOffsetCount(model,category);choices=Enumerable.Range(0,count).Select(i=>new Choice(i.ToString(),gear[offset+i])).ToArray();}else {fields.Add(EV(key,Label(key),value) with {kind="readonly",group=group});continue;}}
            fields.Add(EV(key,Label(key),value,max,choices:choices) with {group=key.StartsWith("Preset")&&key.EndsWith("Index")?"Advanced":group});
        }
        return fields.ToArray();
    }
    ExtraPage ReadPasses(ExtraTool tool,string? detailId) {
        var sav=(SAV4BR)RequireSave();var list=sav.BattlePasses;var rows=new List<ExtraRow>();
        for(int i=0;i<BattlePassAccessor.PASS_COUNT;i++){
            var pass=list[i];string id="pass:"+i;var type=list.GetPassType(i);var actions=new List<Choice>{new("resetpresets","Reset Message Presets")};if(i>0)actions.Add(new("up","Move Up"));if(i<BattlePassAccessor.PASS_COUNT-1)actions.Add(new("down","Move Down"));if(type!=BattlePassType.Rental)actions.Add(new("delete","Delete Pass"));
            rows.Add(ER(id,$"{i+1:000} · {Label(type.ToString())} · {(string.IsNullOrWhiteSpace(pass.Name)?"Empty":pass.Name)}",id==detailId?PassFields(pass):[],pass.Issued?"Issued":pass.Available?"Available":"Locked",actions:actions.ToArray()) with {fileExtension="bin"});
            for(int n=0;n<6;n++){string pid=$"member:{i}:{n}";var pk=pass.GetPartySlotAtIndex(n);var (box,slot)=pass.GetPartySlotBoxSlot(n);rows.Add(ER(pid,$"Pokémon {n+1} · {(pass.GetPartySlotPresent(n)?Species(pk.Species):"Empty")}",detailId==pid?[EV("Box","Linked box",box,255),EV("Slot","Linked slot",slot,255),EV("Flags","Stored flags",pass.GetPartySlotFlags(n),32767),EV("Present","Occupied",pass.GetPartySlotPresent(n))]:[],"The pass stores its own team copy",pass.GetPartySlotPresent(n)?Sprite(pk):"",[new("view","View Pokémon"),new("set","Set Current Pokémon"),new("clear","Remove Pokémon")]) with {fileExtension="bk4"});}
        }
        return new(tool.id,revision,tool,rows.ToArray(),[new("custom","Unlock Custom Passes"),new("rental","Unlock Rental Passes")],true);
    }
    (BattlePass pass,int index,int member) PassTarget(string id){var p=id.Split(':');if(p.Length<2||!int.TryParse(p[1],out int i)||i<0||i>=BattlePassAccessor.PASS_COUNT)throw new Exception("Select a valid pass.");if(p[0]=="pass"&&p.Length==2)return (((SAV4BR)RequireSave()).BattlePasses[i],i,-1);if(p[0]=="member"&&p.Length==3&&int.TryParse(p[2],out int n)&&n>=0&&n<6)return (((SAV4BR)RequireSave()).BattlePasses[i],i,n);throw new Exception("Select a valid pass or team slot.");}
    void EditPass(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        var sav=(SAV4BR)RequireSave();var list=sav.BattlePasses;if(mode=="custom"){list.UnlockAllCustomPasses();return;}if(mode=="rental"){list.UnlockAllRentalPasses();return;}
        var (pass,index,member)=PassTarget(id);
        if(member>=0){
            if(mode=="clear"){pass.DeletePartySlot(member);return;}
            if(mode is "set" or "import"){
                PKM pk;if(mode=="set")pk=entity?.Clone()??throw new Exception("View a Pokémon in the editor first.");else{var data=ReadExtraBytes(S(r,"path"),new BK4().SIZE_STORED);pk=new BK4(data);if(!pk.ChecksumValid)throw new Exception("The Pokémon checksum is invalid.");}
                if(pk.Species==0||pk.Species>493)throw new Exception("Choose a Generation 4 Pokémon.");if(pk is not BK4)pk=EntityConverter.ConvertToType(pk,typeof(BK4),out _)??throw new Exception("This Pokémon cannot convert to Battle Revolution.");
                int target=member;while(target>0&&!pass.GetPartySlotPresent(target-1))target--;WritePassPokemon(pass,pk,target);if(list.GetPassType(index)==BattlePassType.Custom){var (box,slot)=sav.FindSlot(pk);pass.SetPartySlotBoxSlot(target,box,slot);}else if(list.GetPassType(index)==BattlePassType.Rental)pass.SetPartySlotBoxSlot(target,255,0);return;
            }
            var pair=pass.GetPartySlotBoxSlot(member);if(edits.TryGetValue("Box",out var b))pair.Box=byte.Parse(b);if(edits.TryGetValue("Slot",out var s))pair.Slot=byte.Parse(s);pass.SetPartySlotBoxSlot(member,pair.Box,pair.Slot);if(edits.TryGetValue("Flags",out var flags))pass.SetPartySlotFlags(member,ushort.Parse(flags));if(edits.TryGetValue("Present",out var present))pass.SetPartySlotPresent(member,bool.Parse(present));return;
        }
        if(mode=="import"){ReadExtraBytes(S(r,"path"),BattlePass.Size).CopyTo(pass.Data);return;}
        if(mode=="delete"){if(list.GetPassType(index)==BattlePassType.Rental)throw new Exception("Rental passes cannot be deleted.");list.Delete(index);return;}
        if(mode is "up" or "down"){int other=index+(mode=="up"?-1:1);if(other<0||other>=BattlePassAccessor.PASS_COUNT)throw new Exception("The pass is already at the end.");list.Swap(index,other);return;}
        if(mode=="resetpresets"){pass.ResetPresetIndexes();return;}
        if(edits.Remove("Language",out var language)) {
            if(!edits.ContainsKey("SelfIntroduction"))edits["SelfIntroduction"]=pass.SelfIntroduction.TrimStart(StringConverter4GC.Proportional).Replace(StringConverter4GC.LineBreak,'\n');
            pass.Language=(BattlePassLanguage)int.Parse(language);
        }
        if(edits.TryGetValue("RegionCode",out var region)&&region.Any(c=>c>127||c=='\0'))throw new Exception("Use up to four ASCII characters for the region code.");
        foreach(var (key,value) in edits){if(key=="PlayerID"){pass.PlayerID=Convert.ToUInt64(value,16);continue;}if(PassMessages.Contains(key)){int max=PassTextLimits[key]-(key=="SelfIntroduction"&&pass.Language!=BattlePassLanguage.Japanese?2:0);int length=value.Sum(c=>c is '\n' or StringConverter4GC.LineBreak or StringConverter4GC.Proportional or StringConverter4GC.PokemonName?2:1);if(length>max)throw new Exception($"{Label(key)} is too long for this game.");string text=value.Replace('\n',StringConverter4GC.LineBreak);if(key=="SelfIntroduction"&&pass.Language!=BattlePassLanguage.Japanese)text=StringConverter4GC.Proportional+text;SetProperty(pass,key,text);}else SetProperty(pass,key,value);}
        if(edits.ContainsKey("Model")){foreach(var (key,category) in PassGear){var model=(ModelBR)pass.Model;SetProperty(pass,key,(model is >=ModelBR.YoungBoy and <=ModelBR.LittleGirl?GearUnlock.GetDefault(category==GearCategory.Badges?ModelBR.YoungBoy:model,category):0).ToString());}pass.ResetPresetIndexes();}
    }
    // Pass records contain the 136-byte stored BK4 plus four link/flag bytes.
    // The pinned core's party writer expects 220 bytes, so use its stored writer here.
    static void WritePassPokemon(BattlePass pass,PKM pk,int index){pk.WriteEncryptedDataStored(pass.Data.Slice(0x1FC+index*BattlePass.PokeSize,pk.SIZE_STORED));pass.SetPartySlotPresent(index,true);}
    void ViewExtra(JsonElement r){if(N(r,"revision")!=revision)throw new Exception("The save changed. Reopen the entry.");if(S(r,"kind")=="registeredTeams" && RequireSave() is SAV_STADIUM stadium){var parts=S(r,"id").Split(':');var groups=stadium.GetRegisteredTeams();if(parts.Length!=2||!int.TryParse(parts[0],out int t)||!int.TryParse(parts[1],out int m)||t<0||t>=groups.Length||m<0||m>=groups[t].Slots.Length)throw new Exception("Choose a registered team slot.");var pk=groups[t].Slots[m];if(pk.Species==0)throw new Exception("This slot is empty.");entity=pk.Clone();entitySourcePath=null;slot=-1;pending=true;return;}if(S(r,"kind")!="passes")throw new Exception("This entry cannot be loaded as a Pokémon.");var (pass,_,member)=PassTarget(S(r,"id"));if(member<0||!pass.GetPartySlotPresent(member))throw new Exception("Choose an occupied team slot.");entity=pass.GetPartySlotAtIndex(member);entitySourcePath=null;slot=-1;pending=true;}
    byte[] ExportPass(string id){var (pass,_,member)=PassTarget(id);if(member<0)return pass.Data.ToArray();var pk=pass.GetPartySlotAtIndex(member);var data=new byte[pk.SIZE_STORED];pk.WriteDecryptedDataStored(data);return data;}
}
