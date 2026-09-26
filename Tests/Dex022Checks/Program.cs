using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;int checks=0;
object Session(SaveFile save){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,save);return s;}
JsonElement Request(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
SaveFile Current(object s)=>(SaveFile)saveField.GetValue(s)!;
void Assert(bool test,string message){checks++;if(!test)throw new Exception(message);}
void Reject(Action action,string message){try{action();}catch(Exception e)when(e.Message!=message){checks++;return;}throw new Exception(message);}
foreach(var game in new[]{GameVersion.D,GameVersion.P,GameVersion.Pt,GameVersion.HG,GameVersion.SS,GameVersion.B,GameVersion.W,GameVersion.B2,GameVersion.W2,GameVersion.X,GameVersion.Y,GameVersion.OR,GameVersion.AS,GameVersion.SN,GameVersion.MN,GameVersion.US,GameVersion.UM,GameVersion.GP,GameVersion.GE,GameVersion.SW,GameVersion.SH,GameVersion.ZA}){
 SaveFile sav=game switch {GameVersion.D or GameVersion.P=>new SAV4DP(new byte[0x80000]){Version=game},GameVersion.Pt=>new SAV4Pt(new byte[0x80000]),GameVersion.HG or GameVersion.SS=>new SAV4HGSS(new byte[0x80000]){Version=game},_=>BlankSaveFile.Get(game,"Fixture")};var s=Session(sav);type.GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(s,true);type.GetField("sampleVersion",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(s,game);
 foreach(int species in new[]{25,201,327,479,649}.Where(i=>i<=sav.MaxSpeciesID && (sav is not SAV7b||i==25))) {
  if(sav is SAV8SWSH sw&&!sw.Zukan.GetEntry((ushort)species,out _))continue;
  var fields=Request(s,new{op="dexRecord",species}).GetProperty("fields").EnumerateArray().ToArray();Assert(fields.Length>0,"Detailed fields");
  foreach(var field in fields){string id=field.GetProperty("id").GetString()!,value=field.GetProperty("value").GetString()!,kind=field.GetProperty("kind").GetString()!;string next=kind=="bool"?(value=="true"?"false":"true"):kind=="number"?"17":field.GetProperty("choices").EnumerateArray().Select(c=>c.GetProperty("value").GetString()!).FirstOrDefault(v=>v!=value)??value;
    Request(s,new{op="dexRecordSet",species,field=id,value=next});var got=Request(s,new{op="dexRecord",species}).GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==id).GetProperty("value").GetString();Assert(got==next,$"{game} {species} {id}: expected {next}, got {got}");Request(s,new{op="undo"});var restored=Request(s,new{op="dexRecord",species}).GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==id).GetProperty("value").GetString();Assert(restored==value,"Dex Undo");
  }
  Reject(()=>Request(s,new{op="dexRecordSet",species,field="invented",value="true"}),"Unknown dex field");
  if(Current(s) is not (SAV8SWSH or SAV9ZA)){
   var chosen=fields.Last(f=>f.GetProperty("kind").GetString()=="bool");Request(s,new{op="dexRecordSet",species,field=chosen.GetProperty("id").GetString(),value="true"});
   var expected=Request(s,new{op="dexRecord",species}).GetProperty("fields").ToString();var data=Current(s).Write().ToArray();SaveFile copy=Current(s) switch {SAV4DP=>new SAV4DP(data),SAV4Pt=>new SAV4Pt(data),SAV4HGSS=>new SAV4HGSS(data),SAV5BW=>new SAV5BW(data),SAV5B2W2=>new SAV5B2W2(data),SAV6XY=>new SAV6XY(data),SAV6AO=>new SAV6AO(data),SAV7SM=>new SAV7SM(data),SAV7USUM=>new SAV7USUM(data),SAV7b=>new SAV7b(data),_=>throw new Exception("Missing fixture parser")};
   Assert(copy.ChecksumsValid,"Dex checksum "+game);saveField.SetValue(s,copy);Assert(Request(s,new{op="dexRecord",species}).GetProperty("fields").ToString()==expected,"Serialized dex fields "+game);
  }

 }
 Console.WriteLine("PASS "+game+" detailed Pokédex controls and Undo");
}
Console.WriteLine($"PASS {checks} detailed Pokédex requests/assertions; synthetic fixtures");
