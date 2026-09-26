using System.Globalization;
using PKHeX.Core;

sealed partial class EditorSession {
    static ExtraValue RaidHex(string id,string label,ulong value,int digits)=>new(id,label,value.ToString("X"+digits),"hex","0",digits.ToString(),[]);
    (string id,string name,RaidSpawnList8 raids)[] RaidRegions8(){var s=(SAV8SWSH)RequireSave();return [("galar","Galar",s.RaidGalar),("armor","Isle of Armor",s.RaidArmor),("crown","Crown Tundra",s.RaidCrown)];}
    (string id,string name,RaidSpawnList9 raids)[] RaidRegions9(){var s=(SAV9SV)RequireSave();return [("paldea","Paldea",s.RaidPaldea),("kitakami","Kitakami",s.RaidKitakami),("blueberry","Blueberry Academy",s.RaidBlueberry)];}
    ExtraPage ReadRaids(ExtraTool tool,string? detail) {
        var rows=new List<ExtraRow>();
        if(tool.id=="raids8")foreach(var (id,name,list) in RaidRegions8())for(int i=0;i<Math.Min(list.CountUsed,list.CountAll);i++) {
            var r=list.GetRaid(i);string key=$"{id}:{i}";
            rows.Add(ER(key,$"{name} · Den {i+1:000}",key==detail?[
                RaidHex("Hash","Encounter table hash",r.Hash,16),RaidHex("Seed","Raid seed",r.Seed,16),
                EV("Stars","Difficulty",r.Stars,choices:Enumerable.Range(0,5).Select(n=>new Choice(n.ToString(),$"{n+1} star{(n==0?"":"s")}")).ToArray()),
                EP(r,"RandRoll","Encounter roll",100,0),EP(r,"DenType","Den type",choices:ExtraEnums<RaidType>()),EP(r,"WattsHarvested","Watts collected"),EP(r,"IsEvent","Event distribution"),EP(r,"IsRare","Rare encounter"),EP(r,"IsWishingPiece","Wishing Piece used"),EP(r,"Flags","Raw flags",255) with {group="Advanced"}
            ]:[],r.IsActive?$"{r.DenType} · {r.Stars+1} stars · Seed {r.Seed:X16}":"Inactive"));
        }
        else if(tool.id=="raids9")foreach(var (id,name,list) in RaidRegions9()) {
            if(list.CountAll==0)continue;
            if(list.HasSeeds)rows.Add(ER(id+":seeds",name+" · Daily seeds",[RaidHex("CurrentSeed","Today’s seed",list.CurrentSeed,16),RaidHex("TomorrowSeed","Tomorrow’s seed",list.TomorrowSeed,16)]));
            for(int i=0;i<Math.Min(list.CountUsed,list.CountAll);i++) {
                var r=list.GetRaid(i);string key=$"{id}:{i}";
                rows.Add(ER(key,$"{name} · Crystal {i+1:000}",key==detail?[
                    EP(r,"IsEnabled","Active crystal"),RaidHex("Seed","Raid seed",r.Seed,8),EP(r,"Content","Encounter source",choices:[new("0","Standard · up to 5 stars"),new("1","Black crystal · 6 stars"),new("2","Event distribution"),new("3","Mightiest Mark · 7 stars")]),EP(r,"IsClaimedLeaguePoints","League Points collected"),EP(r,"AreaID","Area ID"),EP(r,"LotteryGroup","Location group"),EP(r,"SpawnPointID","Spawn point"),EP(r,"Unused","Unused stored value") with {group="Advanced"}
                ]:[],(r.IsEnabled?"Active":"Inactive")+$" · {r.ScenePointName} · Seed {r.Seed:X8}",actions:[new("copy","Copy Settings to This Region"),new("copyseed","Copy Settings and Seed to This Region")]));
            }
        }
        else {
            var list=((SAV9SV)RequireSave()).RaidSevenStar;
            for(int i=0;i<list.CountAll;i++){var r=list.GetRaid(i);rows.Add(ER(i.ToString(),$"Seven-star record {i+1:0000}",[EP(r,"Identifier","Distribution identifier"),EP(r,"Captured","Captured"),EP(r,"Defeated","Defeated")],$"Event {r.Identifier} · "+(r.Captured?"Captured":r.Defeated?"Defeated":"Not completed")));}
        }
        return new(tool.id,revision,tool,rows.ToArray(),[]);
    }
    void EditRaids(string kind,string id,string mode,Dictionary<string,string> edits) {
        object target;
        if(kind=="sevenstar")target=((SAV9SV)RequireSave()).RaidSevenStar.GetRaid(int.Parse(id));
        else {var parts=id.Split(':');
            if(kind=="raids8") {
                var list=RaidRegions8().Single(x=>x.id==parts[0]).raids;target=list.GetRaid(int.Parse(parts[1]));
                if((edits.ContainsKey("DenType")||edits.ContainsKey("Flags"))&&edits.Keys.Any(k=>k is "IsRare" or "IsWishingPiece" or "IsEvent" or "WattsHarvested"))throw new Exception("Save the den type or raw flags separately from the derived switches, since they share the same stored bits.");
            }
            else {
                var list=RaidRegions9().Single(x=>x.id==parts[0]).raids;
                if(parts[1]=="seeds")target=list;
                else {int i=int.Parse(parts[1]);if(mode is "copy" or "copyseed"){if(list.CountAll<list.CountUsed)throw new Exception("This save has an incomplete raid block; copying would exceed its stored entries.");list.Propagate(i,mode=="copyseed");return;}target=list.GetRaid(i);}
            }
        }
        foreach(var (field,value) in edits)SetProperty(target,field,field is "Seed" or "Hash" or "CurrentSeed" or "TomorrowSeed"?ulong.Parse(value,NumberStyles.HexNumber,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture):value);
    }
}
