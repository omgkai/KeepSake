using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    record MemoryPage(int revision, string entityKey, ExtraRow[] entries);
    static string MemoryKey(PKM pk)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pk.Data));
    ExtraRow[] MemoryRows(PKM pk) {
        if(pk.Format<6)throw new Exception("This format does not store the modern memory and care records.");
        var rows=new List<ExtraRow>();var text=new MemoryStrings(strings);
        Choice[] Choices(IEnumerable<ComboItem> source)=>source.Select(x=>new Choice(x.Value.ToString(),x.Text)).DistinctBy(x=>x.value).ToArray();
        for(int trainer=0;trainer<2;trainer++) {
            bool ot=trainer==0;string prefix=ot?"OriginalTrainer":"HandlingTrainer",name=ot?pk.OriginalTrainerName:pk.HandlingTrainerName;
            var fields=new List<ExtraValue>{EP(pk,prefix+"Friendship","Friendship",255)};
            if(pk is IAffection)fields.Add(EP(pk,prefix+"Affection","Affection",255));
            string description=string.IsNullOrWhiteSpace(name)?"No handling trainer recorded.":$"With {name}";
            if(pk is ITrainerMemories) {
                byte memory=(byte)pk.GetType().GetProperty(prefix+"Memory")!.GetValue(pk)!;int gen=ot?(pk.Generation==0?pk.Format:pk.Generation):pk.Format;
                var argument=Memories.GetMemoryArgType(memory,gen);var options=Choices(text.GetArgumentStrings(argument,gen));
                if(options.Length==1&&options[0].label.Length==0)options=[new("0","None")];
                bool editable=!pk.IsEgg && (!ot || pk.Generation>=6 || pk.Generation==0) && (ot || !string.IsNullOrWhiteSpace(name));
                var memoryFields=new[]{EP(pk,prefix+"Memory","Memory",255,choices:Choices(text.Memory).Select(c=>c with {label=c.label.Split("{4}")[0].Trim().Replace("{0}","your Pokémon").Replace("{1}","its trainer").Replace("{2}","…").Replace("{3}","a feeling").Replace("{4}","an intensity")}).ToArray()),
                    EP(pk,prefix+"MemoryVariable",Label(argument.ToString()),65535,choices:options),
                    EP(pk,prefix+"MemoryIntensity","Intensity",255,choices:text.GetMemoryQualities().ToArray().Select((x,i)=>new Choice(i.ToString(),string.IsNullOrWhiteSpace(x)?"None":x)).Where((x,i)=>memory!=0||i==0).ToArray()),
                    EP(pk,prefix+"MemoryFeeling","Feeling",255,choices:text.GetMemoryFeelings(gen).ToArray().Select((x,i)=>new Choice(i.ToString(),string.IsNullOrWhiteSpace(x)?"None":x)).Where((x,i)=>memory!=0||i==0).ToArray())};
                fields.AddRange(memoryFields.Select(f=>editable?f:f with {kind="readonly"}));
                string LabelValue(ExtraValue f)=>f.choices.FirstOrDefault(c=>c.value==f.value)?.label??$"Stored value {f.value}";
                if(memory<strings.memories.Length) {
                    string template=strings.memories[memory];
                    if(memoryFields[2].value=="0" || memoryFields[3].value=="0")template=template.Split("{4}")[0].Trim();
                    description=string.Format(template,pk.Nickname,name,LabelValue(memoryFields[1]),LabelValue(memoryFields[3]),LabelValue(memoryFields[2]));
                }
                else description=$"Unknown stored memory {memory}.";
                if(!editable)description=pk.IsEgg?"Eggs do not have memories. Stored values are preserved.":ot?"Transferred from an earlier generation; original-trainer memories are preserved.":"No handling trainer recorded; stored memories are preserved.";
            }
            rows.Add(ER(prefix,ot?"Original trainer":"Handling trainer",fields.ToArray(),description));
        }
        var care=new List<ExtraValue>();if(pk is IFullnessEnjoyment){care.Add(EP(pk,"Fullness","Fullness",255));care.Add(EP(pk,"Enjoyment","Enjoyment",255));}if(pk is ISociability)care.Add(EP(pk,"Sociability","Sociability",uint.MaxValue));
        if(care.Count>0)rows.Add(ER("care","Care & companionship",care.ToArray(),"Stored care values travel with this Pokémon. Availability and use differ by game."));
        if(pk is IGeoTrack) {
            var countries=Choices(Util.GetCountryRegionList("countries","en"));var fields=new List<ExtraValue>();
            for(int i=1;i<=5;i++){string p=$"Geo{i}_";int country=(byte)pk.GetType().GetProperty(p+"Country")!.GetValue(pk)!;fields.Add(EP(pk,p+"Country",$"Residence {i} · Country",255,choices:countries));fields.Add(EP(pk,p+"Region",$"Residence {i} · Region",255,choices:country==0?[new("0","None")]:Choices(Util.GetCountryRegionList($"sr_{country:000}","en"))));}
            rows.Add(ER("residences","Residence history",fields.ToArray(),"Five stored country and region pairs, ordered from most recent to oldest."));
        }
        return rows.ToArray();
    }
    PKM MemoryCandidate(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The Pokémon changed. Reopen Memories & Care before saving.");
        var original=RequireEntity();if(S(r,"entityKey")!=MemoryKey(original))throw new Exception("The selected Pokémon changed. Reopen Memories & Care.");var pk=original.Clone();string id=S(r,"id");var initial=MemoryRows(original).FirstOrDefault(x=>x.id==id)??throw new Exception("Choose an available memory or care card.");
        var edits=new Dictionary<string,string>();foreach(var e in r.GetProperty("edits").EnumerateArray()){string field=S(e,"field"),value=S(e,"value");if(!initial.fields.Any(x=>x.id==field&&x.kind!="readonly")||!edits.TryAdd(field,value))throw new Exception("Unknown, read-only or duplicate memory field.");}
        // Resolve dependent catalogs on a clone. Changing a memory or country resets its dependent values.
        foreach(var (key,value) in edits.Where(x=>x.Key.EndsWith("Memory")||x.Key.EndsWith("_Country"))) {
            var f=initial.fields.Single(x=>x.id==key);if(!f.choices.Any(c=>c.value==value)&&value!=f.value)throw new Exception("Choose a listed memory or country.");
            SetProperty(pk,key,value);if(value==f.value)continue;
            if(key.EndsWith("Memory")){foreach(string suffix in new[]{"Variable","Intensity","Feeling"})SetProperty(pk,key+suffix,"0");}
            else SetProperty(pk,key.Replace("_Country","_Region"),"0");
        }
        var schema=MemoryRows(pk).Single(x=>x.id==id);
        foreach(var f in schema.fields.Where(f=>f.id.EndsWith("MemoryVariable"))) {
            string parent=f.id[..^8];
            if(edits.TryGetValue(parent,out var selected) && selected!=initial.fields.Single(x=>x.id==parent).value && !edits.ContainsKey(f.id) && f.choices.Length>0 && !f.choices.Any(c=>c.value==f.value))SetProperty(pk,f.id,f.choices[0].value);
        }
        schema=schema with {fields=schema.fields.Select(f=>f with {value=initial.fields.Single(x=>x.id==f.id).value}).ToArray()};
        foreach(var (key,value) in ExtraEdits(r,schema))SetProperty(pk,key,value);
        if(pk is ITrainerMemories) {
            foreach(string prefix in new[]{"OriginalTrainer","HandlingTrainer"})if(edits.ContainsKey(prefix+"Memory")) {
                byte memory=(byte)pk.GetType().GetProperty(prefix+"Memory")!.GetValue(pk)!;
                int gen=prefix=="OriginalTrainer"?(pk.Generation==0?pk.Format:pk.Generation):pk.Format;
                if(memory==0){foreach(string suffix in new[]{"Variable","Intensity","Feeling"})SetProperty(pk,prefix+"Memory"+suffix,"0");}
                else if(Memories.GetMemoryArgType(memory,gen)==MemoryArgType.None)SetProperty(pk,prefix+"MemoryVariable","0");
            }
        }
        pk.RefreshChecksum();return pk;
    }
    object ReadMemoryPage(JsonElement r)=>new MemoryPage(revision,MemoryKey(RequireEntity()),MemoryRows(r.TryGetProperty("edits",out _)?MemoryCandidate(r):RequireEntity()));
    void SetMemoryPage(JsonElement r){entity=MemoryCandidate(r);pending=true;}
}
