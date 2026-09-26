using System.Globalization;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    record ZAStore(string id,string name,int count,bool flag,bool tuple,Func<int,ulong[]> keys,Action<int,ulong[]> setKeys,Func<int,string> value,Action<int,string> setValue,Action<int> clear,Action compress);
    record ZAEventPage(int revision,string group,Choice[] groups,int count,int total,int offset,ExtraRow[] entries);
    readonly Dictionary<ulong,string> zaEventNames=new(){{FnvHash.HashEmpty,""}};
    static ZAStore ZA64<T>(string id,string name,EventWorkStorage64<T> s) where T:struct,IEquatable<T> => new(id,name,s.Count,typeof(T)==typeof(bool),false,i=>[s.GetKey(i)],(i,k)=>s.SetKey(i,k[0]),i=>Convert.ToString(s.GetValue(i),CultureInfo.InvariantCulture)!.ToLowerInvariant(),(i,v)=>s.SetValue(i,(T)(typeof(T)==typeof(bool)?(object)bool.Parse(v):ulong.Parse(v,CultureInfo.InvariantCulture))),s.Clear,s.Compress);
    static ZAStore ZA128(string id,string name,EventWorkValueStorageKey128 s,bool tuple=false)=>new(id,name,s.Count,false,tuple,i=>{var k=s.GetKey(i);return[k.A,k.B];},(i,k)=>s.SetKey(i,k[0],k[1]),i=>s.GetValue(i).ToString(CultureInfo.InvariantCulture),(i,v)=>s.SetValue(i,ulong.Parse(v,CultureInfo.InvariantCulture)),s.Clear,s.Compress);
    static ZAStore ZA192(string id,string name,EventWorkValueStorageKey192 s)=>new(id,name,s.Count,false,false,i=>{var k=s.GetKey(i);return[k.A,k.B,k.C];},(i,k)=>s.SetKey(i,k[0],k[1],k[2]),i=>s.GetValue(i).ToString(CultureInfo.InvariantCulture),(i,v)=>s.SetValue(i,ulong.Parse(v,CultureInfo.InvariantCulture)),s.Clear,s.Compress);
    static ZAStore[] ZAStores(SAV9ZA sav) {var b=sav.Blocks;return[ZA64("Flags","System flags",b.Flags),ZA64("Event","Event flags",b.Event),ZA64("Work","System values",b.Work),ZA64("Quest","Quest progress",b.Quest),ZA64("WorkMable","Mable tasks",b.WorkMable),ZA64("CountMable","Mable counters",b.CountMable),ZA64("CountTitle","Title counters",b.CountTitle),ZA128("Report","Reports",b.Report,true),ZA64("WorkSpawn","World spawns",b.WorkSpawn),ZA64("InfiniteRank","Infinite rank",b.InfiniteRank),ZA128("Spawner2","Spawn state",b.Spawner2,true),ZA128("Spawner4","Spawn variants",b.Spawner4),ZA128("Obstruction","Obstructions",b.Obstruction,true),ZA64("FieldItems","Field items",b.FieldItems),ZA192("FieldObjectInteractable","Interactive objects",b.FieldObjectInteractable)];}
    string ZAName(ulong key)=>zaEventNames.GetValueOrDefault(key,key.ToString("X16"));
    ulong ZAHash(string text) {text=text.Trim();if(text.Length==0)return FnvHash.HashEmpty;var known=zaEventNames.FirstOrDefault(x=>x.Value==text);if(!known.Equals(default(KeyValuePair<ulong,string>)))return known.Key;return ulong.TryParse(text.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?text[2..]:text,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var key)?key:FnvHash.HashFnv1a_64(text);}
    static bool ZAEmpty(ulong[] keys)=>keys.All(k=>k==FnvHash.HashEmpty);
    ExtraRow ZARow(ZAStore s,int i) {
        var keys=s.keys(i);bool empty=ZAEmpty(keys);var fields=keys.Select((k,n)=>s.tuple&&n==1?EV("key1","Additional value",k,ulong.MaxValue):EV("key"+n,keys.Length==1?"Name or hexadecimal key":$"Key {(char)('A'+n)} · name or hex",ZAName(k)) with {kind="text",max="256"}).ToList();
        fields.Add(s.flag?EV("value","Enabled",bool.Parse(s.value(i))):EV("value","Value",s.value(i),ulong.MaxValue));
        return ER(i.ToString(),empty?$"Empty entry {i}":string.Join(" · ",keys.Select((k,n)=>s.tuple&&n==1?k.ToString():ZAName(k))),fields.ToArray(),$"Entry {i} · {s.value(i)}",actions:[new("clear","Remove Entry")]);
    }
    object ReadZAEvents(JsonElement r) {
        if(RequireSave() is not SAV9ZA sav)throw new Exception("Open a Legends: Z-A save.");var stores=ZAStores(sav);string group=S(r,"group");var s=stores.FirstOrDefault(x=>x.id==group)??stores[0];string search=S(r,"search");bool empty=B(r,"showEmpty");int offset=Math.Max(0,N(r,"offset"));
        var indices=Enumerable.Range(0,s.count).Where(i=>(empty||!ZAEmpty(s.keys(i)))&&(string.IsNullOrEmpty(search)||i.ToString()==search||string.Join(" ",s.keys(i).Select(ZAName)).Contains(search,StringComparison.OrdinalIgnoreCase)||s.value(i).Contains(search,StringComparison.OrdinalIgnoreCase))).ToArray();
        return new ZAEventPage(revision,s.id,stores.Select(x=>new Choice(x.id,x.name)).ToArray(),s.count,indices.Length,offset,indices.Skip(offset).Take(100).Select(i=>ZARow(s,i)).ToArray());
    }
    void SetZAEvent(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Reopen this event record.");if(RequireSave() is not SAV9ZA sav)throw new Exception("Open a Legends: Z-A save.");var s=ZAStores(sav).FirstOrDefault(x=>x.id==S(r,"group"))??throw new Exception("Choose an event group.");int i=N(r,"index");if(i<0||i>=s.count)throw new Exception("Choose an existing entry.");
        if(S(r,"mode")=="clear"){s.clear(i);s.compress();dirty=true;return;}
        if(S(r,"mode")!="edit")throw new Exception("Choose Edit or Remove Entry.");
        var row=ZARow(s,i);var edits=ExtraEdits(r,row);var keys=s.keys(i);foreach(var (field,value) in edits){if(field=="value")s.setValue(i,value);else{int part=int.Parse(field[3..]);keys[part]=s.tuple&&part==1?ulong.Parse(value,CultureInfo.InvariantCulture):ZAHash(value);}}
        s.setKeys(i,keys);s.compress();dirty=true;
    }
    object LoadZAEventNames(JsonElement r) {
        var file=new FileInfo(S(r,"path"));if(!file.Exists||file.Length>4*1024*1024)throw new Exception("Choose a key-name text file smaller than 4 MiB.");var next=new Dictionary<ulong,string>{{FnvHash.HashEmpty,""}};SCBlockMetadata.AddExtraKeyNames64(next,File.ReadLines(file.FullName));zaEventNames.Clear();foreach(var pair in next)zaEventNames[pair.Key]=pair.Value;return new{count=next.Count-1};
    }
    Dictionary<string,(int index,string section,string name,string value)> ZASnapshot(SAV9ZA sav) {
        var result=new Dictionary<string,(int,string,string,string)>();
        foreach(var s in ZAStores(sav))for(int i=0;i<s.count;i++) {
            var keys=s.keys(i);if(ZAEmpty(keys))continue;
            var identity=s.tuple?keys.Take(1):keys;string root=s.id+":"+string.Join(":",identity.Select(k=>k.ToString("X16"))),id=root;int duplicate=0;while(result.ContainsKey(id))id=root+":"+(++duplicate);
            result[id]=(i,s.name,string.Join(" · ",identity.Select(ZAName)),(s.tuple?keys[1]+" / ":"")+s.value(i));
        }
        return result;
    }
    object CompareZAEvents(JsonElement r) {
        eventComparison=null;eventComparisonPaths=[];string previous=S(r,"previous"),updated=S(r,"updated");
        if(ReadEventComparisonSave(previous) is not SAV9ZA old || (string.IsNullOrEmpty(updated)?RequireSave():ReadEventComparisonSave(updated)) is not SAV9ZA next)throw new Exception("Choose Legends: Z-A saves for this comparison.");
        var before=ZASnapshot(old);var after=ZASnapshot(next);var changes=new List<EventDifference>();int unchanged=0;
        foreach(var id in before.Keys.Union(after.Keys)) {bool had=before.TryGetValue(id,out var a),has=after.TryGetValue(id,out var b);if(had&&has&&a.value==b.value){unchanged++;continue;}var row=has?b:a;changes.Add(new(id,row.index,row.section,row.name,had?a.value:"Not present",has?b.value:"Removed"));}
        string left=Path.GetFileName(previous),right=string.IsNullOrEmpty(updated)?"Current workspace":Path.GetFileName(updated);var report=new System.Text.StringBuilder($"KeepSake Z-A event comparison\nBefore: {left}\nAfter: {right}\n{changes.Count} changed · {unchanged} unchanged\n\n");foreach(var c in changes)report.AppendLine($"{c.section}: {c.name}\n  {c.before} → {c.after}");
        eventComparisonPaths=new[]{previous,updated}.Where(x=>!string.IsNullOrEmpty(x)).Select(Path.GetFullPath).ToArray();return eventComparison=new(Guid.NewGuid().ToString("N"),left,right,unchanged,changes.ToArray(),report.ToString());
    }

}
