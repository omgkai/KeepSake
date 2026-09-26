using PKHeX.Core;

sealed partial class EditorSession
{
    static readonly (string key,string label)[] UndergroundScores = [
        ("UG_PeopleMet","Players met"),("UG_GiftsGiven","Gifts given"),("UG_GiftsReceived","Gifts received"),
        ("UG_Spheres","Spheres dug up"),("UG_Fossils","Fossils dug up"),("UG_TrapPlayers","Players caught in traps"),
        ("UG_TrapSelf","Traps triggered"),("UG_MyBaseMoved","Base moves"),("UG_FlagsTaken","Flags obtained"),
        ("UG_FlagsFromMe","My flag taken"),("UG_FlagsRecovered","My flag recovered"),
        ("UG_FlagsCaptured","Flags captured"),("UG_HelpedOthers","Players helped")];
    string[] UndergroundNames(string pouch)=>pouch switch {
        "goods"=>strings.uggoods,"spheres"=>strings.ugspheres,"traps"=>strings.ugtraps,"treasures"=>strings.ugtreasures,
        _=>throw new Exception("Unknown Underground pouch.")
    };
    static Span<byte> UndergroundPouch(SAV4Sinnoh s,string pouch)=>pouch switch {
        "goods"=>s.GetUGI_Goods(),"spheres"=>s.GetUGI_Spheres(),"traps"=>s.GetUGI_Traps(),"treasures"=>s.GetUGI_Treasures(),
        _=>throw new Exception("Unknown Underground pouch.")
    };
    ExtraPage ReadUnderground4(ExtraTool tool)
    {
        var s=(SAV4Sinnoh)RequireSave();var rows=new List<ExtraRow>();
        rows.Add(ER("records","Explorer records",UndergroundScores.Select(x=>EP(s,x.key,x.label,SAV4Sinnoh.UG_MAX)).ToArray(),"Your Underground adventures, gifts and capture-the-flag records."));
        foreach(string pouch in new[]{"goods","spheres","traps","treasures"}) {
            var data=UndergroundPouch(s,pouch);var names=UndergroundNames(pouch);
            var choices=names.Select((name,i)=>new Choice(i.ToString(),i==0?"Empty":name)).Where(c=>!string.IsNullOrWhiteSpace(c.label)).ToArray();
            int owned=0;for(int i=0;i<SAV4Sinnoh.UG_POUCH_SIZE;i++)if(data[i]!=0)owned++;
            var actions=new List<Choice>{new("compact","Pack Empty Slots"),new("clear","Empty This Pouch")};
            if(choices.Length-1<=SAV4Sinnoh.UG_POUCH_SIZE)actions.Insert(0,new("give","Give Every Type"));
            rows.Add(ER("pouch:"+pouch,Label(pouch)+" pouch",[], $"{owned} of 40 slots occupied. Each item uses one slot."+(pouch=="spheres"?" Give Every Type supplies size-99 spheres.":""),actions:actions.ToArray()));
            for(int i=0;i<SAV4Sinnoh.UG_POUCH_SIZE;i++) {
                byte item=data[i];string name=item==0?"Empty":item<names.Length&&!string.IsNullOrWhiteSpace(names[item])?names[item]:$"Stored item {item}";
                var fields=new List<ExtraValue>{EV("Item","Item",item,choices:choices)};
                if(pouch=="spheres")fields.Add(EV("Size","Sphere size",data[i+SAV4Sinnoh.UG_POUCH_SIZE],255));
                rows.Add(ER(pouch+":"+i,$"{i+1:00} · {name}",fields.ToArray(),Label(pouch)+" · Empty slots are packed when you save an item."));
            }
        }
        return new("underground4",revision,tool,rows.ToArray(),[]);
    }
    void EditUnderground4(string id,string mode,Dictionary<string,string> edits)
    {
        var s=(SAV4Sinnoh)RequireSave();
        if(id=="records"){SetExtraProperties(s,edits);return;}
        var parts=id.Split(':');bool summary=parts[0]=="pouch";string pouch=summary?parts[1]:parts[0];
        var data=UndergroundPouch(s,pouch);bool spheres=pouch=="spheres";const int count=SAV4Sinnoh.UG_POUCH_SIZE;
        if(mode=="clear"){data.Clear();return;}
        if(mode=="give") {
            var types=UndergroundNames(pouch).Select((name,i)=>(name,i)).Where(x=>x.i>0&&!string.IsNullOrWhiteSpace(x.name)).ToArray();
            if(types.Length>count)throw new Exception("This collection does not fit in one pouch.");
            data.Clear();for(int i=0;i<types.Length;i++){data[i]=checked((byte)types[i].i);if(spheres)data[i+count]=99;}return;
        }
        if(mode=="edit") {
            int index=int.Parse(parts[1]);
            if(edits.TryGetValue("Item",out var item))data[index]=byte.Parse(item);
            if(edits.TryGetValue("Size",out var size))data[index+count]=byte.Parse(size);
        }
        // Keep sphere sizes paired with their item, including imported unknown IDs.
        int next=0;for(int i=0;i<count;i++)if(data[i]!=0){data[next]=data[i];if(spheres)data[next+count]=data[i+count];next++;}
        data.Slice(next,count-next).Clear();if(spheres)data.Slice(count+next,count-next).Clear();
    }
}
