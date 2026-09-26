using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;var session=Activator.CreateInstance(type,new object[]{null!})!;var ef=type.GetField("entity",BindingFlags.Instance|BindingFlags.NonPublic)!;int checks=0;
PKM Pk()=>(PKM)ef.GetValue(session)!;
JsonElement Req(object payload){checks++;using var d=JsonDocument.Parse(JsonSerializer.Serialize(payload));try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(session,new object[]{d.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
void Assert(bool b,string why){checks++;if(!b)throw new Exception(why);}
string S(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
JsonElement Info(string field)=>Req(new{op="nameBytesInfo",field});
JsonElement Preview(JsonElement p,string mode,string text="",string hex=null,int species=25,int language=2,int generation=6)=>Req(new{op="nameBytesInfo",field=S(p,"field"),revision=p.GetProperty("revision").GetInt32(),entityKey=S(p,"entityKey"),hex=hex??S(p,"hex"),mode,text,species,language,generation});
void Set(JsonElement p)=>Req(new{op="nameBytesSet",field=S(p,"field"),revision=p.GetProperty("revision").GetInt32(),entityKey=S(p,"entityKey"),hex=S(p,"hex")});
void Reject(Action action){bool failed=false;try{action();}catch{failed=true;}Assert(failed,"Expected rejection");}
var games=Req(new{op="sampleGames"}).EnumerateArray().Select(x=>S(x,"value")).ToArray();
foreach(string game in games){
 Req(new{op="demo",version=game});var first=Info("Nickname");
 foreach(var field in first.GetProperty("fields").EnumerateArray().Select(f=>S(f,"value"))){
  string shortName=Pk() is GBPKM {Japanese:true}?"アイ":"AB";
  var original=Info(field);string before=S(original,"hex");var edited=Preview(original,"text",shortName);Assert(S(Info(field),"hex")==before,"Preview changed live bytes");Assert(S(edited,"text")==shortName,"Text encoding "+game+field);Set(edited);Assert(S(Info(field),"hex")==S(edited,"hex"),"Exact commit");Req(new{op="undo"});Assert(S(Info(field),"hex")==before,"Exact undo");Req(new{op="redo"});Assert(S(Info(field),"text")==shortName,"Redo");
  var current=Info(field);var layer=Preview(current,"layer",species:151,language:Pk() is GBPKM {Japanese:true}?1:2,generation:Math.Max(1,(int)Pk().Generation));Assert(S(layer,"text")==shortName,"Layer changed visible name");Set(layer);var clear=Preview(Info(field),"clear");Assert(S(clear,"text")==shortName,"Clear changed visible text");Set(clear);Assert(Pk().ChecksumValid,"Checksum");
  current=Info(field);Reject(()=>Preview(current,"hex",hex:"GG"));Reject(()=>Preview(current,"text",new string('A',100)));Reject(()=>Preview(current,"layer",species:0));Assert(S(Info(field),"hex")==S(current,"hex"),"Failure atomicity");
  foreach(var character in current.GetProperty("characters").EnumerateArray()){var p=Preview(current,"text",S(character,"value"));Assert(S(p,"hex").Length==S(current,"hex").Length,"Character storage width");}
 }
 Console.WriteLine("PASS "+game+" text, byte previews, layers, clearing, characters, Undo/Redo");
}
Req(new{op="demo",version="SL"});var stale=Info("Nickname");Req(new{op="select",box=0,slot=1,party=false});Reject(()=>Set(stale));Reject(()=>Info("Species"));
Console.WriteLine($"PASS {checks} name-byte requests/assertions");
