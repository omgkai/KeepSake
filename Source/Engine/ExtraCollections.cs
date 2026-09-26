using System.Globalization;
using System.Text.Json;
using PKHeX.Core;
sealed partial class EditorSession {
    void SetSelectedMedals(JsonElement r) {
        if(save is not SAV5B2W2 || N(r,"revision")!=revision)throw new Exception("Refresh the medal collection.");
        var ids=r.GetProperty("ids").EnumerateArray().Select(x=>x.GetString()!).Distinct().ToArray();if(ids.Length is <1 or >256)throw new Exception("Select medals to edit.");
        foreach(var id in ids) {
            if(!int.TryParse(id,out _))throw new Exception("Choose medal entries, not collection settings.");
            var request=new Dictionary<string,object?>{{"kind","medals"},{"id",id},{"revision",revision},{"mode",S(r,"mode","edit")}};
            if(r.TryGetProperty("edits",out var edits))request["edits"]=edits.Clone();
            EditExtra(JsonSerializer.SerializeToElement(request));
        }
    }
    ExtraPage ReadMedals(ExtraTool tool) {
        var m=((SAV5B2W2)save!).Medals;var names=Util.GetStringList("medals","en");var rows=new List<ExtraRow>{ER("settings","Medal box",[EP(m,"PinnedMedal","Pinned medal",choices:names.Select((n,i)=>new Choice(i.ToString(),n)).Append(new("255","None")).ToArray()),EP(m,"Rank",choices:ExtraEnums<MedalRank5>()),EP(m,"IsTutorialComplete","Tutorial complete")],$"{m.GetCountObtained()} medals obtained",actions:[new("rank","Calculate Rank")])};
        for(int i=0;i<names.Length;i++){var medal=m[i];string date="";try{if(medal.HasDate)date=medal.Date.ToString("yyyy-MM-dd");}catch(ArgumentOutOfRangeException){}rows.Add(ER(i.ToString(),names[i],[EP(medal,"State",choices:ExtraEnums<MedalState5>()),EP(medal,"IsUnread","Unread"),new ExtraValue("Date","Award date",date,"date","","",[])],Label(MedalList5.GetMedalType(i).ToString()),actions:[new("clear","Clear Medal")]));}
        return new(tool.id,revision,tool,rows.ToArray(),[new("give","Give All Medals")],true);
    }
    void EditMedals(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        var m=((SAV5B2W2)save!).Medals;
        if(mode=="give"){m.GiveAll(EncounterDate.GetDateNDS(),true);return;}
        if(mode=="import"){ReadExtraBytes(S(r,"path"),MedalList5.LengthAllMedals).CopyTo(m.AllMedals);return;}
        if(mode=="rank"){m.Rank=m.CalculateRank();return;}
        if(id=="settings"){SetExtraProperties(m,edits);return;}
        var medal=m[int.Parse(id)];if(mode=="clear"){medal.Clear();return;}
        if(edits.TryGetValue("State",out var state)&&state!=((int)medal.State).ToString())medal.State=(MedalState5)int.Parse(state);
        if(edits.TryGetValue("IsUnread",out var unread))medal.IsUnread=bool.Parse(unread);
        if(edits.TryGetValue("Date",out var date)&&date!=""){if(!medal.CanHaveDate)throw new Exception("Only a received medal or hint can have an award date.");medal.Date=DateOnly.ParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture);}
        else if(medal.CanHaveDate&&!medal.HasDate)medal.Date=EncounterDate.GetDateNDS();
    }
    ExtraPage ReadHabitats(ExtraTool tool) {
        var h=((SAV5B2W2)save!).Medals.HabitatList;var rows=new List<ExtraRow>{ER("settings","Habitat tracking",[EP(h,"IsTutorialViewed","Tutorial viewed"),EP(h,"IsTutorialCompleteCapture","Capture tutorial complete"),EP(h,"LastEncounterType","Last encounter type",choices:ExtraEnums<HabitatEncounterType5>()),EP(h,"Unknown90","Additional counter 90",65535),EP(h,"Unknown92","Additional counter 92",255)],"The additional counters retain PKHeX's labels because their game meaning is unknown.")};
        for(int i=0;i<HabitatList5.HabitatCount;i++){var v=h.GetHabitat(i);rows.Add(ER(i.ToString(),"Habitat "+(i+1),[EP(v,"Grass",choices:ExtraEnums<HabitatCompletion5>()),EP(v,"Surf","Water",choices:ExtraEnums<HabitatCompletion5>()),EP(v,"Fish","Fishing",choices:ExtraEnums<HabitatCompletion5>()),EP(v,"IsComplete","Complete")],actions:[new("complete","Complete Habitat"),new("clear","Clear Habitat")]));}
        return new(tool.id,revision,tool,rows.ToArray(),[new("give","Complete All Habitats")]);
    }
    void EditHabitats(string id,string mode,Dictionary<string,string> edits) {
        var h=((SAV5B2W2)save!).Medals.HabitatList;if(mode=="give"){h.CompleteAll();return;}if(id=="settings"){SetExtraProperties(h,edits);return;}
        var entry=h.GetHabitat(int.Parse(id));if(mode=="complete")entry.SetComplete();else if(mode=="clear")entry.Clear();else SetExtraProperties(entry,edits);
    }
    ExtraPage ReadDonuts(ExtraTool tool,string? selected) {
        var donuts=((SAV9ZA)save!).Donuts;var rows=new List<ExtraRow>();bool detail=!string.IsNullOrEmpty(selected);int start=detail?int.Parse(selected!):0,end=detail?start+1:DonutPocket9a.MaxCount;if(start<0||end>DonutPocket9a.MaxCount)throw new Exception("Choose a valid donut slot.");
        Choice[] berries=detail?ItemStorage9ZA.Berry.ToArray().Select(i=>new Choice(i.ToString(),MoveLabel(strings.itemlist,i))).Prepend(new("0","None")).ToArray():[];
        Choice[] powers=detail?DonutInfo.Flavors.Select((f,i)=>new Choice(f.Hash.ToString(),MoveLabel(strings.donutFlavor,i))).Prepend(new("0","None")).ToArray():[];
        Choice[] names=detail?strings.donutName.Select((n,i)=>new Choice(i.ToString(),n)).ToArray():[];
        for(int i=start;i<end;i++){var d=donuts.GetDonut(i);var fields=new List<ExtraValue>();if(detail){fields.AddRange([EP(d,"Donut","Recipe",choices:names),EP(d,"Stars",max:5),EP(d,"Calories",max:9999),EP(d,"LevelBoost","Level boost",255),EP(d,"MillisecondsSince1970","Creation date",253402300799999) with {kind="timestamp"}]);foreach(string b in new[]{"BerryName","Berry1","Berry2","Berry3","Berry4","Berry5","Berry6","Berry7","Berry8"})fields.Add(EP(d,b,b=="BerryName"?"Berry shown in name":Label(b),choices:berries));for(int f=0;f<3;f++)fields.Add(EP(d,"Flavor"+f,"Power "+(f+1),choices:powers));}
            var profile=new int[5];d.RecalculateDonutFlavors(profile);string name=d.MillisecondsSince1970==0?"Empty donut slot":MoveLabel(strings.donutName,d.Donut);
            rows.Add(ER(i.ToString(),$"{i+1:000} · {name}",fields.ToArray(),$"{d.Stars} stars · {d.Calories} cal · Flavor profile: Spicy {profile[0]}, Fresh {profile[1]}, Sweet {profile[2]}, Bitter {profile[3]}, Sour {profile[4]}",sprite:DonutSpriteName(d),actions:[new("clear","Clear Donut"),new("recalculate","Calculate Stats from Berries"),new("clone","Copy to Every Donut Slot")],profile:profile));}
        return new(tool.id,revision,tool,rows.ToArray(),[new("random","Fill with Random Level 3 Powers"),new("shiny","Fill with Shiny Assortment")],true);
    }
    static string DonutSpriteName(Donut9a d)=>d.Donut switch {198=>"donut_uni491",199=>"donut_uni383",200=>"donut_uni382",201=>"donut_uni384",202=>"donut_uni807",_=>"donut_"+new[]{"sweet","spicy","sour","bitter","fresh","mix"}[d.Donut%6]+Math.Min((byte)5,d.Stars).ToString("D2")};
    void EditDonuts(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        var donuts=((SAV9ZA)save!).Donuts;if(mode=="random"){donuts.SetAllRandomLv3();return;}if(mode=="shiny"){donuts.SetAllAsShinyTemplate();return;}
        int index=int.Parse(id);if(index<0||index>=DonutPocket9a.MaxCount)throw new Exception("Choose a valid donut slot.");var donut=donuts.GetDonut(index);
        if(mode=="clone")donuts.CloneAllFromIndex(index);
        else if(mode=="clear")donut.Clear();
        else if(mode=="import") { if(r.TryGetProperty("hex",out _))ParseNameHex(S(r,"hex"),Donut9a.Size).CopyTo(donut.Data);else ReadExtraBytes(S(r,"path"),Donut9a.Size).CopyTo(donut.Data); }
        else if(mode=="recalculate")donut.RecalculateDonutStats();
        else {SetExtraProperties(donut,edits);if(donut.MillisecondsSince1970==0 && (donut.Donut!=0 || donut.Stars!=0 || donut.Calories!=0 || donut.LevelBoost!=0 || donut.GetBerries().ToArray().Any(x=>x!=0) || donut.GetFlavors().ToArray().Any(x=>x!=0)))throw new Exception("Set a creation date to mark this donut slot as occupied, or use Clear Donut to empty it.");if(edits.ContainsKey("MillisecondsSince1970")){if(donut.MillisecondsSince1970==0)donut.ClearDateTime();else donut.UpdateDateTime();}}
    }
    static byte[] ReadExtraBytes(string path,int size){if(new FileInfo(path).Length!=size)throw new Exception($"This file must contain exactly {size} bytes.");return File.ReadAllBytes(path);}
    object ExportExtra(JsonElement r) {
        string kind=S(r,"kind");if(!ExtraTools().Any(t=>t.id==kind))throw new Exception("This collection is unavailable.");byte[] bytes;
        if(kind=="misc5" && S(r,"id")=="forestcity" && RequireSave() is SAV5BW bw)bytes=bw.Forest.ForestCity.ToArray();else if(kind=="chatter")bytes=ExportChatter(r);else if(kind=="passes")bytes=ExportPass(S(r,"id"));else if(kind=="link6"){if(S(r,"id")!="settings")throw new Exception("Choose the Pokémon Link delivery.");bytes=((ISaveBlock6Main)RequireSave()).Link.Gifts.Data.ToArray();}else if(kind is "dlc4" or "dlc5")bytes=DownloadBytes(S(r,"id"),B(r,"decrypted"));else if(kind=="festival")bytes=ExportAgency(S(r,"id"));else if(kind=="bases6")bytes=ExportBase(S(r,"id"));else if(kind=="avenue")bytes=AvenueEntity(S(r,"id")).Write().ToArray();else if(kind=="medals")bytes=((SAV5B2W2)save!).Medals.AllMedals.ToArray();else if(kind=="donuts"){int index=int.Parse(S(r,"id"));if(index<0||index>=DonutPocket9a.MaxCount)throw new Exception("Choose a valid donut slot.");bytes=((SAV9ZA)save!).Donuts.GetDonut(index).Data.ToArray();}else throw new Exception("This editor does not export a separate file.");
        var path=ExportPath(S(r,"path"),sourcePath);AtomicWrite(path,bytes);return new{path};
    }
}
