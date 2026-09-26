using System.Reflection;
using PKHeX.Core;

sealed partial class EditorSession {
    // Only callers with explicitly listed, supported models use these scalar controls.
    ExtraValue[] SportsFields(object obj) {
        var result=new List<ExtraValue>();
        foreach(var p in obj.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.CanWrite&&p.GetIndexParameters().Length==0)) {
            if(p.Name=="ID32")continue;var v=p.GetValue(obj);var id=p.Name;
            if(v is string text){result.Add(ET(id,Label(id),text,7));continue;}
            if(v is not (byte or ushort or uint or bool))continue;
            decimal max=v is byte?255:v is ushort?65535:uint.MaxValue;
            if(obj is PokeathlonGlobalCounters4)max=id=="TimeSpent"?59999:id=="Fame"?65535:9999999;
            if(id=="Attempts")max=9999999;
            if(id=="Form")max=31;
            var choices=id=="Species"?Enumerable.Range(0,494).Select(n=>new Choice(n.ToString(),Species((ushort)n))).ToArray():id=="Gender"?new[]{new Choice("0","Boy ♂"),new Choice("1","Girl ♀"),new Choice("2","Genderless")}:id=="Language"?GameInfo.LanguageDataSource(4,EntityContext.Gen4).Select(x=>new Choice(x.Value.ToString(),x.Text)).ToArray():null;
            result.Add(EV(id,Label(id),v,max,choices:choices));
        }return result.ToArray();
    }
    Pokeathlon4 Sports=>((SAV4HGSS)RequireSave()).Pokeathlon;
    ExtraPage ReadSports(ExtraTool tool,string? detailId) {
        var p=Sports;var rows=new List<ExtraRow>();
        var general=new List<ExtraValue>{EP(p,"Points",max:99999)};
        for(int i=0;i<27;i++)general.Add(EV("card:"+i,strings.itemlist[505+i],(p.FlagsDataCard&(1u<<i))!=0) with {group="Data cards"});
        for(int i=0;i<12;i++)general.Add(EV("shop:"+i,$"Shop purchase {i+1}",(p.FlagsDailyShop&(1<<i))!=0) with {group="Daily shop"});
        rows.Add(ER("general","Points, data cards & daily shop",general.ToArray(),$"{p.Points:N0} points",actions:[new("cards","Give All Data Cards"),new("resetshop","Reset Daily Shop")]));
        rows.Add(ER("counters","Lifetime records",SportsFields(p.GlobalCounters),$"Global score {p.CalculateGlobalScore():N0} · {Pokeathlon4.CalculateFriendshipTrophyCount(p.CalculateGlobalScore())} friendship trophies"));
        for(int c=0;c<5;c++) {
            var course=p.GetCourseRecord((PokeathlonStat4)c);string name=Label(((PokeathlonStat4)c).ToString());
            rows.Add(ER($"course:{c}",name+" course",SportsFields(course),"Scores and winning team"));
            for(int i=0;i<3;i++){var pk=course.GetParticipant(i);rows.Add(ER($"participant:{c}:{i}",$"{name} · Pokémon {i+1}",detailId==$"participant:{c}:{i}"?SportsFields(pk):[],Species(pk.Species),SpriteFor(pk.Species,pk.Form,pk.Gender,0,save!.Context,pk.IsShiny)));}
        }
        for(int e=0;e<10;e++) {
            var ev=(PokeathlonEvent4)e;string name=Label(ev.ToString());
            rows.Add(ER($"best:{e}",name+" · best score",[EV("Score","Best score",p.GetBestScore(ev),65535),EV("First","First-place finishes",p.GlobalCounters[ev],9999999)]));
            foreach(bool connected in new[]{false,true}){
                string group=connected?"connected":"self";var data=connected?p.GetEventConnection(ev).Inner:p.GetEventSelf(ev);
                rows.Add(ER($"{group}:{e}",name+(connected?" · linked play":" · solo play"),[EP(data,"Attempts",max:9999999)],"Attempts and five ranked records"));
                for(int n=0;n<5;n++) {
                    string id=$"record:{group}:{e}:{n}";var r=data.GetRecord(n);var fields=new List<ExtraValue>{EP(r,"Record",max:65535)};
                    if(id==detailId)for(int k=0;k<3;k++){var f=k==0?r.Entry0:k==1?r.Entry1:r.Entry2;fields.Add(EV($"Species:{k}",$"Pokémon {k+1}",f.Species,choices:Enumerable.Range(0,494).Select(s=>new Choice(s.ToString(),Species((ushort)s))).ToArray()));fields.Add(EV($"Form:{k}",$"Form {k+1}",f.Form,63));}
                    rows.Add(ER(id,$"{name} · {(connected?"Linked":"Solo")} rank {n+1}",fields.ToArray(),ev==PokeathlonEvent4.HurdleDash?"Time in frames; lower is better.":"Stored event score and participating Pokémon"));
                    if(connected){var t=p.GetEventConnection(ev).GetTrainer(n);rows.Add(ER($"trainer:{e}:{n}",$"{name} · trainer {n+1}",SportsFields(t),t.OriginalTrainerName));}
                }
            }
        }
        for(ushort s=1;s<=493;s++){byte bits=p.Medals.GetMedal(s);rows.Add(ER("medal:"+s,Species(s),Enumerable.Range(0,5).Select(i=>EV("Medal:"+i,Label(((PokeathlonStat4)i).ToString()),(bits&(1<<i))!=0)).ToArray(),"Course medals",SpriteFor(s,0,0,0,save!.Context)));}
        return new(tool.id,revision,tool,rows.ToArray(),[new("medals","Give All Medals"),new("clearmedals","Clear All Medals")]);
    }
    void EditSports(string id,string mode,Dictionary<string,string> edits) {
        var p=Sports;
        if(mode=="medals"||mode=="clearmedals"){p.Medals.SetAllMedals(mode=="medals"?(byte)31:(byte)0);return;}
        if(mode=="cards"){p.FlagsDataCard=Pokeathlon4.DataCardAllObtained;return;}
        if(mode=="resetshop"){p.FlagsDailyShop=0;return;}
        string[] parts=id.Split(':');int i=parts.Length>1&&int.TryParse(parts[1],out int parsed)?parsed:0;
        if(id=="general"){foreach(var (key,v) in edits){if(key=="Points")p.Points=uint.Parse(v);else if(key.StartsWith("card:")){uint mask=1u<<int.Parse(key[5..]);p.FlagsDataCard=bool.Parse(v)?p.FlagsDataCard|mask:p.FlagsDataCard&~mask;}else{ushort mask=(ushort)(1<<int.Parse(key[5..]));p.FlagsDailyShop=(ushort)(bool.Parse(v)?p.FlagsDailyShop|mask:p.FlagsDailyShop&~mask);}}return;}
        if(id=="counters"){SetExtraProperties(p.GlobalCounters,edits);return;}
        switch(parts[0]) {
            case "course":SetExtraProperties(p.GetCourseRecord((PokeathlonStat4)i),edits);break;
            case "participant":SetExtraProperties(p.GetCourseRecord((PokeathlonStat4)i).GetParticipant(int.Parse(parts[2])),edits);break;
            case "trainer":SetExtraProperties(p.GetEventConnection((PokeathlonEvent4)i).GetTrainer(int.Parse(parts[2])),edits);break;
            case "best":foreach(var (k,v) in edits){if(k=="Score")p.SetBestScore((PokeathlonEvent4)i,ushort.Parse(v));else{var c=p.GlobalCounters;c[(PokeathlonEvent4)i]=uint.Parse(v);}}break;
            case "self":SetExtraProperties(p.GetEventSelf((PokeathlonEvent4)i),edits);break;
            case "connected":SetExtraProperties(p.GetEventConnection((PokeathlonEvent4)i).Inner,edits);break;
            case "record":var ev=(PokeathlonEvent4)int.Parse(parts[2]);var data=parts[1]=="self"?p.GetEventSelf(ev):p.GetEventConnection(ev).Inner;var r=data.GetRecord(int.Parse(parts[3]));foreach(var (k,v) in edits){if(k=="Record"){r.Record=ushort.Parse(v);continue;}int n=int.Parse(k.Split(':')[1]);var f=n==0?r.Entry0:n==1?r.Entry1:r.Entry2;if(k.StartsWith("Species"))f.Species=ushort.Parse(v);else f.Form=byte.Parse(v);if(n==0)r.Entry0=f;else if(n==1)r.Entry1=f;else r.Entry2=f;}break;
            case "medal":byte bits=p.Medals.GetMedal((ushort)i);foreach(var (k,v) in edits){int mask=1<<int.Parse(k.Split(':')[1]);bits=(byte)(bool.Parse(v)?bits|mask:bits&~mask);}p.Medals.SetMedal((ushort)i,bits);break;
        }
    }
}
