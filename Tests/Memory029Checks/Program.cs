using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var ef=type.GetField("entity",BindingFlags.Instance|BindingFlags.NonPublic)!;
int checks=0;
object session=Activator.CreateInstance(type,new object[]{null!})!;
JsonElement Req(object payload){checks++;using var d=JsonDocument.Parse(JsonSerializer.Serialize(payload));try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(session,new object[]{d.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
PKM Pk()=>(PKM)ef.GetValue(session)!;
int Rev()=>Req(new{op="state"}).GetProperty("revision").GetInt32();
JsonElement Page()=>Req(new{op="memoryInfo"});
JsonElement Row(string id)=>Page().GetProperty("entries").EnumerateArray().Single(x=>x.GetProperty("id").GetString()==id);
JsonElement[] Fields(JsonElement row)=>row.GetProperty("fields").EnumerateArray().ToArray();
string Val(string id,string field)=>Fields(Row(id)).Single(f=>f.GetProperty("id").GetString()==field).GetProperty("value").GetString()!;
JsonElement Edit(string op,string id,params (string field,string value)[] edits)=>Req(new{op,id,revision=Rev(),entityKey=Page().GetProperty("entityKey").GetString(),edits=edits.Select(e=>new{e.field,e.value})});
void Assert(bool b,string why){checks++;if(!b)throw new Exception(why);}
void Reject(Action action){var before=Pk().Data.ToArray();var rev=Rev();bool failed=false;try{action();}catch{failed=true;}Assert(failed,"Expected rejection");Assert(before.SequenceEqual(Pk().Data),"Rejected edit mutated bytes");Assert(rev==Rev(),"Rejected edit changed revision");}
foreach(string game in new[]{"X","SN","GP","SW","BD","PLA","SL","ZA"}){
 Req(new{op="demo",version=game});Pk().HandlingTrainerName="Friend";Pk().RefreshChecksum();
 var before=Pk().Data.ToArray();Page();Assert(before.SequenceEqual(Pk().Data),game+" read purity");
 foreach(var r in Page().GetProperty("entries").EnumerateArray()){
  string id=r.GetProperty("id").GetString()!;
  foreach(var f in Fields(r).Where(f=>f.GetProperty("kind").GetString()=="number")){
   string field=f.GetProperty("id").GetString()!,max=f.GetProperty("max").GetString()!;
   before=Pk().Data.ToArray();Edit("memoryInfo",id,(field,max));Assert(before.SequenceEqual(Pk().Data),"Preview purity");
   Edit("memorySet",id,(field,max));Assert(Val(id,field)==max,game+" "+field+" max roundtrip");
   Req(new{op="undo"});Assert(before.SequenceEqual(Pk().Data),"Undo exact");Req(new{op="redo"});Assert(Val(id,field)==max,"Redo");
   Reject(()=>Edit("memorySet",id,(field,"-1")));Reject(()=>Edit("memorySet",id,(field,(ulong.Parse(max)+1).ToString())));
  }
 }
 if(Pk() is ITrainerMemories){
  foreach(string id in new[]{"OriginalTrainer","HandlingTrainer"}){
   var choices=Fields(Row(id)).Single(f=>f.GetProperty("id").GetString()==id+"Memory").GetProperty("choices").EnumerateArray().ToArray();
   foreach(var c in choices){
    string memory=c.GetProperty("value").GetString()!;
    before=Pk().Data.ToArray();var preview=Edit("memoryInfo",id,(id+"Memory",memory));Assert(before.SequenceEqual(Pk().Data),"Memory preview purity");
    Assert(preview.GetProperty("entries").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id).GetProperty("detail").GetString()!.Length>0,"Memory narrative");
    var row=preview.GetProperty("entries").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==id);
    var argument=Fields(row).Single(f=>f.GetProperty("id").GetString()==id+"MemoryVariable");
    Assert(argument.GetProperty("choices").EnumerateArray().Any(c=>c.GetProperty("value").GetString()==argument.GetProperty("value").GetString()),"Changed memory has a listed argument");
    var values=new List<(string,string)>{(id+"Memory",memory)};
    foreach(var f in Fields(row).Where(f=>f.GetProperty("id").GetString()!.StartsWith(id+"Memory")&&f.GetProperty("id").GetString()!=id+"Memory")){
     var options=f.GetProperty("choices").EnumerateArray().ToArray();if(options.Length>0)values.Add((f.GetProperty("id").GetString()!,options.Last().GetProperty("value").GetString()!));
    }
    Edit("memorySet",id,values.ToArray());Assert(Val(id,id+"Memory")==memory,"Memory roundtrip");
    Assert(Pk().ChecksumValid,"Memory checksum");
   }
   Edit("memorySet",id,(id+"Memory","0"));foreach(string suffix in new[]{"Variable","Intensity","Feeling"})Assert(Val(id,id+"Memory"+suffix)=="0","Clear memory");
   Reject(()=>Edit("memorySet",id,(id+"Memory","255")));
  }
 }
 if(Pk() is IGeoTrack){
  for(int i=1;i<=5;i++){
   string p=$"Geo{i}_";var country=Fields(Row("residences")).Single(f=>f.GetProperty("id").GetString()==p+"Country").GetProperty("choices").EnumerateArray().First(c=>c.GetProperty("value").GetString()=="49").GetProperty("value").GetString()!;
   var page=Edit("memoryInfo","residences",(p+"Country",country));var region=Fields(page.GetProperty("entries").EnumerateArray().Single(r=>r.GetProperty("id").GetString()=="residences")).Single(f=>f.GetProperty("id").GetString()==p+"Region").GetProperty("choices").EnumerateArray().Last().GetProperty("value").GetString()!;
   Edit("memorySet","residences",(p+"Country",country),(p+"Region",region));Assert(Val("residences",p+"Region")==region,"Residence roundtrip");Edit("memorySet","residences",(p+"Country","0"));Assert(Val("residences",p+"Region")=="0","Country resets region");
  }
 }
 Reject(()=>Edit("memorySet","OriginalTrainer",("Species","1")));
 Reject(()=>Edit("memorySet","OriginalTrainer",("OriginalTrainerFriendship","1"),("OriginalTrainerFriendship","2")));
 Reject(()=>Req(new{op="memorySet",id="OriginalTrainer",revision=-1,edits=new[]{new{field="OriginalTrainerFriendship",value="5"}}}));
 byte[] data=new byte[Pk().SIZE_PARTY];Pk().WriteDecryptedDataParty(data);var copy=EntityFormat.GetFromBytes(data,EntityContext.None)!;Assert(copy.ChecksumValid&&copy.Species==Pk().Species,"Serialized readback");
 Console.WriteLine("PASS "+game+" memories, care, preview, validation and undo");
}
Req(new{op="demo",version="X"});Pk().IsEgg=true;Reject(()=>Edit("memorySet","OriginalTrainer",("OriginalTrainerMemory","1")));
Pk().IsEgg=false;Pk().HandlingTrainerName="";Reject(()=>Edit("memorySet","HandlingTrainer",("HandlingTrainerMemory","1")));
Pk().Version=GameVersion.B;Reject(()=>Edit("memorySet","OriginalTrainer",("OriginalTrainerMemory","1")));
Req(new{op="demo",version="B2"});Reject(()=>Page());
Console.WriteLine($"PASS {checks} memory requests/assertions");
