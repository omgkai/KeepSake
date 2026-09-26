using PKHeX.Core;

sealed partial class EditorSession {
    static ExtraValue ET(string id,string label,string value,int length)=>new(id,label,value,"text","0",length.ToString(),[]);
    Choice[] FameSpecies()=>Enumerable.Range(0,RequireSave().MaxSpeciesID+1).Select(i=>new Choice(i.ToString(),Species((ushort)i))).ToArray();
    ExtraPage ReadFame(ExtraTool tool,string? detail) {
        var rows=new List<ExtraRow>();Choice[] actions=[];var species=FameSpecies();
        ExtraValue SpeciesValue(ushort value)=>EV("Species","Species",value,choices:species);
        if(save is SAV1 one) {
            rows.Add(ER("count","League clears",[EV("Count","Total clears",one.HallOfFameCount,255)]));
            for(int team=0;team<50;team++) {
                rows.Add(ER($"team:{team}",$"Team {team+1}",[],one.HallOfFame.GetTeamSummary(team,strings.specieslist),actions:team>0?[new("delete","Delete Team")]:[]));
                for(int slot=0;slot<6;slot++){var pk=one.HallOfFame.GetEntity(team,slot);var id=$"{team}:{slot}";rows.Add(ER(id,$"Team {team+1} · {slot+1} · "+(pk.IsEmpty?"Empty":pk.Nickname),id==detail?[SpeciesValue(pk.Species),EV("Level","Level",pk.Level,255),ET("Nickname","Nickname",pk.Nickname,one.Japanese?5:10)]:[],pk.IsEmpty?"Empty":Species(pk.Species)+$" · Lv. {pk.Level}",SpriteFor(pk.Species,0,0,0,one.Context),slot>0?[new("clear","Clear Slot")]:[]));}
            }
            actions=[new("register","Register Current Party"),new("clearall","Clear Hall of Fame")];
        } else if(save is SAV3 three) {
            var entries=HallFame3Entry.GetEntries(three);
            for(int team=0;team<entries.Length;team++) {
                rows.Add(ER($"team:{team}",$"Team {team+1}",[],string.Join(" · ",entries[team].Team.Where(p=>p.Species>0).Select(p=>p.Nickname)),actions:[new("party","Use Current Party")]));
                for(int slot=0;slot<6;slot++){var pk=entries[team].GetMember(slot);var id=$"{team}:{slot}";rows.Add(ER(id,$"Team {team+1} · {slot+1} · "+(pk.Species==0?"Empty":pk.Nickname),id==detail?[SpeciesValue(pk.Species),EP(pk,"Level",max:100),ET("Nickname","Nickname",pk.Nickname,three.Japanese?5:10),EP(pk,"TID16","Trainer ID",65535),EP(pk,"SID16","Secret ID",65535),EP(pk,"PID","Personality ID")]:[],Species(pk.Species)+$" · Lv. {pk.Level}"+(pk.IsShiny?" · Shiny":""),SpriteFor(pk.Species,pk.DisplayForm(three.Version),0,0,three.Context,pk.IsShiny),[new("clear","Clear Slot")]));}
            }
            actions=[new("partyall","Use Current Party for All Teams")];
        } else if(save is SAV7 seven) {
            var fame=seven.EventWork.Fame;
            for(int i=0;i<12;i++){ushort spec=fame.GetEntry(i);rows.Add(ER(i.ToString(),$"{(i<6?"First clear":"Current clear")} · {i%6+1}",[SpeciesValue(spec)],Species(spec),SpriteFor(spec,0,0,0,seven.Context)));}
            if(seven is SAV7USUM us)rows.Add(ER("starter","Starter Pokémon",[EP(us.Misc,"StarterEncryptionConstant","Encryption constant")],"Ultra Sun and Ultra Moon's stored starter identity."));
        } else if(save is ISaveBlock6Main six) {
            var sav=(SAV6)save!;var fame=six.HallOfFame;
            var moves=Enumerable.Range(0,sav.MaxMoveID+1).Select(i=>new Choice(i.ToString(),strings.movelist[i])).ToArray();
            var items=sav.HeldItems.ToArray().Select(i=>new Choice(i.ToString(),strings.itemlist[i])).Prepend(new("0","None")).DistinctBy(c=>c.value).ToArray();
            for(int team=0;team<HallOfFame6.Entries;team++) {
                var index=new HallFame6Index(fame.GetEntry(team)[^4..]);string date=$"{index.Year+2000:D4}-{index.Month:D2}-{index.Day:D2}";if(!DateOnly.TryParse(date,out _))date="";
                rows.Add(ER($"team:{team}",team==0?"First Hall of Fame clear":$"Recent team {team}",[EV("HasData","Record present",index.HasData),EV("ClearIndex","Clear number",index.ClearIndex,9999),new("Date","Clear date",date,"date","","",[])],index.HasData?$"Clear {index.ClearIndex} · {date}":"Empty record",actions:team>0?[new("delete","Delete Team")]:[]));
                for(int slot=0;slot<6;slot++) {
                    var pk=new HallFame6Entity(fame.GetEntity(team,slot),sav.Language);var id=$"{team}:{slot}";
                    ExtraValue[] fields=id==detail?[SpeciesValue(pk.Species),EV("Form","Form",pk.Form,choices:FormConverter.GetFormList(pk.Species,strings.Types,strings.forms,sav.Context).Select((name,i)=>new Choice(i.ToString(),string.IsNullOrWhiteSpace(name)?"Normal":name)).ToArray()),EV("Level","Level",pk.Level,100),EV("Gender","Gender",pk.Gender,choices:[new("0","Male"),new("1","Female"),new("2","Genderless")]),EV("IsShiny","Shiny",pk.IsShiny),EV("IsNicknamed","Nicknamed",pk.IsNicknamed),ET("Nickname","Nickname",pk.Nickname,12),ET("OriginalTrainerName","Original trainer",pk.OriginalTrainerName,12),EV("OriginalTrainerGender","Trainer gender",pk.OriginalTrainerGender,choices:[new("0","Male"),new("1","Female")]),EV("TID16","Trainer ID",pk.TID16,65535),EV("SID16","Secret ID",pk.SID16,65535),EV("EncryptionConstant","Encryption constant",pk.EncryptionConstant),EV("HeldItem","Held item",pk.HeldItem,choices:items),EV("Move1","Move 1",pk.Move1,choices:moves),EV("Move2","Move 2",pk.Move2,choices:moves),EV("Move3","Move 3",pk.Move3,choices:moves),EV("Move4","Move 4",pk.Move4,choices:moves)]:[];
                    rows.Add(ER(id,$"Team {team+1} · {slot+1} · "+(pk.Species==0?"Empty":pk.Nickname),fields,Species(pk.Species)+$" · Lv. {pk.Level}",SpriteFor(pk.Species,pk.Form,(byte)pk.Gender,0,sav.Context,pk.IsShiny)));
                }
            }
        }
        return new(tool.id,revision,tool,rows.ToArray(),actions);
    }
    void EditFame(string id,string mode,Dictionary<string,string> edits) {
        var parts=id.Split(':');int team=parts.Length==2?int.Parse(parts[0]=="team"?parts[1]:parts[0]):0;int slot=parts.Length==2&&parts[0]!="team"?int.Parse(parts[1]):0;
        if(save is SAV1 one) {
            var fame=one.HallOfFame;
            if(mode=="register"){if(one.PartyCount==0)throw new Exception("Add Pokémon to the party first.");one.HallOfFameCount=fame.RegisterParty(one,one.HallOfFameCount);}
            else if(mode=="clearall"){fame.Clear();one.HallOfFameCount=0;}
            else if(mode=="delete")fame.Delete(team);
            else if(id=="count")one.HallOfFameCount=byte.Parse(edits["Count"]);
            else {var pk=fame.GetEntity(team,slot);if(mode=="clear")pk.Clear();else {foreach(var (key,value) in edits)switch(key){case "Species":pk.Species=ushort.Parse(value);break;case "Level":pk.Level=byte.Parse(value);break;case "Nickname":pk.Nickname=value;break;}if(pk.Species==0)pk.Clear();}}
        } else if(save is SAV3 three) {
            var entries=HallFame3Entry.GetEntries(three);
            if(mode is "party" or "partyall") {if(three.PartyCount==0)throw new Exception("Add Pokémon to the party first.");PKM[] party=Enumerable.Range(0,6).Select(i=>i<three.PartyCount?three.GetPartySlotAtIndex(i):new PK3()).ToArray();if(mode=="partyall")foreach(var t in entries)t.CopyFrom(party);else entries[team].CopyFrom(party);}
            else {var pk=entries[team].GetMember(slot);if(mode=="clear")pk.Data.Clear();else SetExtraProperties(pk,edits);}
            HallFame3Entry.SetEntries(three,entries);
        } else if(save is SAV7 seven) {if(id=="starter")SetExtraProperties(((SAV7USUM)seven).Misc,edits);else seven.EventWork.Fame.SetEntry(int.Parse(id),ushort.Parse(edits["Species"]));}
        else if(save is ISaveBlock6Main six) {
            var fame=six.HallOfFame;
            if(mode=="delete"){if(team==0)throw new Exception("The first Hall of Fame clear cannot be deleted.");fame.ClearEntry(team);}
            else if(parts[0]=="team") {var index=new HallFame6Index(fame.GetEntry(team)[^4..]);foreach(var (key,value) in edits)switch(key){case "HasData":index.HasData=bool.Parse(value);break;case "ClearIndex":index.ClearIndex=uint.Parse(value);break;case "Date":if(value==""){index.Year=index.Month=index.Day=0;}else{var date=DateOnly.ParseExact(value,"yyyy-MM-dd");index.Year=(uint)(date.Year-2000);index.Month=(uint)date.Month;index.Day=(uint)date.Day;}break;}if(index.HasData&&!DateOnly.TryParse($"{index.Year+2000:D4}-{index.Month:D2}-{index.Day:D2}",out _))throw new Exception("Set a valid clear date for this record.");}
            else {var pk=new HallFame6Entity(fame.GetEntity(team,slot),save!.Language);foreach(var (key,value) in edits)switch(key){
                case "Species":pk.Species=ushort.Parse(value);break;case "Form":pk.Form=byte.Parse(value);break;case "Level":pk.Level=uint.Parse(value);break;case "Gender":pk.Gender=uint.Parse(value);break;case "IsShiny":pk.IsShiny=bool.Parse(value);break;case "IsNicknamed":pk.IsNicknamed=bool.Parse(value);break;case "Nickname":pk.Nickname=value;break;case "OriginalTrainerName":pk.OriginalTrainerName=value;break;case "OriginalTrainerGender":pk.OriginalTrainerGender=uint.Parse(value);break;case "TID16":pk.TID16=ushort.Parse(value);break;case "SID16":pk.SID16=ushort.Parse(value);break;case "EncryptionConstant":pk.EncryptionConstant=uint.Parse(value);break;case "HeldItem":pk.HeldItem=ushort.Parse(value);break;case "Move1":pk.Move1=ushort.Parse(value);break;case "Move2":pk.Move2=ushort.Parse(value);break;case "Move3":pk.Move3=ushort.Parse(value);break;case "Move4":pk.Move4=ushort.Parse(value);break;
            }}
        }
    }
}
