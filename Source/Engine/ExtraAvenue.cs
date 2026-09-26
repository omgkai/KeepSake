using System.Globalization;
using System.Reflection;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    JoinAvenue5 Avenue => ((SAV5B2W2)RequireSave()).JoinAvenue;
    IJoinAvenueEntity5 AvenueEntity(string id) {
        if(id=="self")return Avenue.Self;
        var p=id.Split(':');
        if(p.Length!=2 || !int.TryParse(p[1],out int i) || i<0)throw new Exception("Choose an existing Join Avenue person.");
        return p[0] switch {
            "visitor" when i<8=>Avenue.GetVisitor(i),"shop" when i<8=>Avenue.GetOccupant(i),
            "fan" when i<12=>Avenue.GetFan(i),"assistant" when i<4=>Avenue.GetAssistant(i),
            _=>throw new Exception("Choose an existing Join Avenue person.")
        };
    }
    ExtraPage ReadAvenue(ExtraTool tool,string? detailId) {
        var rows=new List<ExtraRow>();var settings=Avenue.Settings;
        rows.Add(ER("settings","Your avenue",[..AvenueFields(settings),EP(Avenue,"CountVisitor","Stored visitor count") with {group="Advanced"},EP(Avenue,"CountFan","Stored fan count") with {group="Advanced"},EP(Avenue,"ScriptFlag","Script update flag") with {group="Advanced"}],$"{settings.Name} · Rank {settings.Rank}",actions:[new("resetvisits","Reset Today’s Visitor List")]));
        rows.Add(ER("self","Your visitor profile",detailId=="self"?AvenueFields(Avenue.Self):[],"The profile shared when you visit another avenue.") with {fileExtension="jav5"});
        foreach(var (key,title,count) in new[]{("shop","Shop",8),("visitor","Visitor",8),("fan","Fan",12),("assistant","Assistant",4)})for(int i=0;i<count;i++) {
            string id=$"{key}:{i}";var person=AvenueEntity(id);var visitor=person as JoinAvenueVisitor5;
            string detail=visitor!=null&&key=="shop"?$"{AvenueShopLabel(visitor.ShopType)} · Rank {visitor.ShopRank}":person.IsInteractedToday?"Already interacted today":"Ready for a visit";
            rows.Add(ER(id,$"{title} {i+1} · {(string.IsNullOrWhiteSpace(person.Name)?"Empty":person.Name)}",detailId==id?AvenueFields(person):[],detail,visitor?.FavoriteSpecies is >0 and <=649?SpriteFor(visitor.FavoriteSpecies,0,0,0,save!.Context):"") with {fileExtension=person.FileExtension});
        }
        for(int i=0;i<32;i++){uint tid=settings.GetVisitingPlayerTrainerID(i);rows.Add(ER($"history:{i}",$"Remembered visitor {i+1}",[EV("TID","Trainer ID",tid&65535,65535),EV("SID","Secret ID",tid>>16,65535)],tid==uint.MaxValue?"Empty":$"Trainer {tid&65535:D5} · Secret {tid>>16:D5}"));}
        return new("avenue",revision,tool,rows.ToArray(),[],true);
    }
    static string AvenueShopLabel(ushort raw) {
        if(raw==0)return "No shop";
        if(raw>320)return $"Stored shop · {raw}";
        int n=raw-1;return $"{(JoinAvenueShopType5)(n/10%8)} · Level {n%10+1} · Variant {n/80+1}";
    }
    ExtraValue[] AvenueFields(object obj) {
        var fields=new List<ExtraValue>();
        var countries=Util.GetCountryRegionList("gen5_countries","en");
        // Property setters come from a fixed set of PKHeX Join Avenue models. Packed aliases
        // are omitted so saving one visible value cannot silently overwrite another.
        foreach(var p in obj.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.CanWrite)) {
            string id=p.Name;if(id=="PlayedTime"||id.EndsWith("Tuple"))continue;
            object value=p.GetValue(obj)!;string group=AvenueGroup(id,obj);
            if(value is JoinAvenueDate5 date){AddDate(id,id=="Date1"?"Unidentified date":Label(id),date,id=="Date1"?"Advanced":"Dates");continue;}
            if(value is string str){fields.Add(ET(id,Label(id),str,obj is JoinAvenueSettings5?20:id=="Name"?7:8) with {group=group});continue;}
            if(value is not (byte or ushort or uint or bool or Enum))continue;
            decimal max=value is byte?255:value is ushort?65535:uint.MaxValue;
            if(id.StartsWith("ShopCount"))max=15;
            max=id switch{"Unknown22" or "Gender" or "UnknownBits28_31"=>15,"PlayedHours" or "DexSeen" or "FavoriteSpecies"=>1023,"PlayedMinutes"=>63,"JoinAvenueLevel" or "UnknownBits21_27"=>127,"ShopRank"=>10,"UnknownBits0_8"=>511,"UnknownBits10"=>7,"Rank"=>9999,"VisitingPlayerDatabaseCount"=>32,"VistiingPlayerDatabaseInsertIndex"=>31,_=>max};
            Choice[]? choices=id switch {
                "Country"=>Enumerable.Range(0,256).Select(n=>new Choice(n.ToString(),n==0?"Not set":countries.FirstOrDefault(c=>c.Value==n)?.Text??$"Country code {n}")).ToArray(),
                "ShopType" or "DesiredShopType"=>Enumerable.Range(0,321).Select(n=>new Choice(n.ToString(),AvenueShopLabel((ushort)n))).ToArray(),
                "FavoriteSpecies" or "Species"=>Enumerable.Range(0,650).Select(n=>new Choice(n.ToString(),Species((ushort)n))).ToArray(),
                "Gender"=>[new("0","Boy ♂"),new("1","Girl ♀"),new("2","Unspecified")],
                "Origin"=>[new("0","Game character"),new("1","Player")],
                "Version"=>GameInfo.FilteredSources.Games.Where(g=>g.Value<=255).Select(g=>new Choice(g.Value.ToString(),g.Text)).ToArray(),
                "Language"=>GameInfo.LanguageDataSource(5,EntityContext.Gen5).Select(g=>new Choice(g.Value.ToString(),g.Text)).ToArray(),
                "CeilingColor"=>ExtraEnums<JoinAvenueCeilingColor5>(),_=>null
            };
            string label=id switch{"TID16"=>"Trainer ID","Sprite"=>"Overworld appearance ID","IsInteractedToday"=>"Already interacted today","IsInventory"=>"Inventory state","MetYear"=>"Met year (since 2000)","VistiingPlayerDatabaseInsertIndex"=>"Next visitor record slot",_=>Label(id)};
            fields.Add(EV(id,label,value,max,choices:choices) with {group=group});
        }
        if(obj is JoinAvenueVisitor5 v) {
            for(int i=0;i<8;i++)fields.Add(EV($"record:{i}",Label(((JoinAvenueRecordIndex5)i).ToString()),v.GetRecord((JoinAvenueRecordIndex5)i)) with {group="Records"});
            for(int i=0;i<16;i++)fields.Add(EV($"trivia:{i}",$"Trivia answer {i+1}",v.GetTrivia(i),255) with {group="Advanced"});
            for(int i=0;i<4;i++){fields.Add(EV($"activity:{i}",$"Activity {i+1}",v.GetActivity(i),255) with {group="Activities"});AddDate($"activityDate:{i}",$"Activity {i+1} date",v.GetActivityDate(i),"Activities");}
        }
        return fields.ToArray();
        void AddDate(string id,string label,JoinAvenueDate5 date,string group) {
            fields.Add(new(id,label,date.Date?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??"","date","0","0",[],group));
            fields.Add(EV("raw:"+id,label+" · raw stored value",date.RawValue,65535) with {group="Advanced"});
        }
    }
    static string AvenueGroup(string id,object target) {
        if(id.Contains("Unknown")||id.StartsWith("Unused")||id.StartsWith("IsFlag")||id is "Seed" or "Flags" or "PositionUnused" or "Sprite" or "ShopWork" or "BubbleTarget")return "Advanced";
        if(id.StartsWith("Shop")||id is "DesiredShopType" or "IsShopChangeAllowed" or "IsInventory")return "Shop";
        if(id.StartsWith("Met"))return "Dates";
        if(id.StartsWith("Medal")||id is "DexSeen" or "JoinAvenueRank" or "JoinAvenueLevel")return "Adventure";
        if(id.StartsWith("Position"))return "Position";
        if(id.Contains("PlayerDatabase"))return "Visitor history";
        return target is JoinAvenueSettings5?"Avenue settings":"Profile";
    }
    void EditAvenue(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        if(mode=="import") {
            var target=AvenueEntity(id);string path=S(r,"path");long size=new FileInfo(path).Length;
            if(size is not (JoinAvenueVisitor5.SIZE or JoinAvenueFan5.SIZE or JoinAvenueAssistant5.SIZE))throw new Exception("Choose a Join Avenue visitor (.jav5), fan (.jah5), or assistant (.jaa5) file.");
            byte[] data=ReadExtraBytes(path,(int)size);
            IJoinAvenueEntity5 source=size switch{JoinAvenueVisitor5.SIZE=>new JoinAvenueVisitor5(data),JoinAvenueFan5.SIZE=>new JoinAvenueFan5(data),_=>new JoinAvenueAssistant5(data)};
            target.CopyFrom(source);return;
        }
        if(id=="settings"){if(mode=="resetvisits")Avenue.Settings.ResetPlayerVisitList();else foreach(var (key,value) in edits)SetProperty(key is "CountVisitor" or "CountFan" or "ScriptFlag"?(object)Avenue:Avenue.Settings,key,value);return;}
        if(id.StartsWith("history:")) {
            int i=int.Parse(id[8..]);if(i<0||i>=32)throw new Exception("Choose an existing history slot.");
            uint old=Avenue.Settings.GetVisitingPlayerTrainerID(i);
            uint tid=edits.TryGetValue("TID",out var t)?uint.Parse(t):old&65535,sid=edits.TryGetValue("SID",out var s)?uint.Parse(s):old>>16;
            Avenue.Settings.SetVisitingPlayerTrainerID(i,tid|(sid<<16));return;
        }
        var person=AvenueEntity(id);
        foreach(var field in edits.Keys.Where(k=>k.StartsWith("raw:")))if(edits.ContainsKey(field[4..]))throw new Exception("Edit either the date or its raw stored value in one change, not both.");
        // Strings use the selected language regardless of the ordering of submitted fields.
        if(edits.TryGetValue("Language",out var language))person.Language=byte.Parse(language);
        foreach(var (key,value) in edits) {
            string field=key.StartsWith("raw:")?key[4..]:key;
            if(person is JoinAvenueVisitor5 v) {
                if(field.StartsWith("record:")){v.SetRecord((JoinAvenueRecordIndex5)int.Parse(field[7..]),uint.Parse(value));continue;}
                if(field.StartsWith("trivia:")){v.SetTrivia(int.Parse(field[7..]),byte.Parse(value));continue;}
                if(field.StartsWith("activity:")){v.SetActivity(int.Parse(field[9..]),byte.Parse(value));continue;}
                if(field.StartsWith("Date")||field.StartsWith("activityDate:")) {
                    var date=key.StartsWith("raw:")?new JoinAvenueDate5(ushort.Parse(value)):new JoinAvenueDate5(0){Date=value.Length==0?null:DateOnly.ParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture)};
                    if(field.StartsWith("activityDate:"))v.SetActivityDate(int.Parse(field[13..]),date);else typeof(JoinAvenueVisitor5).GetProperty(field)!.SetValue(v,date);continue;
                }
            }
            SetProperty(person,field,value);
        }
    }
}
