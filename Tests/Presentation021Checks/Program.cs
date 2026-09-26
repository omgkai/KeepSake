using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;int checks=0;
object Session(SaveFile save){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,save);return s;}
JsonElement Request(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
SaveFile Current(object s)=>(SaveFile)saveField.GetValue(s)!;
int Revision(object s)=>Request(s,new{op="state"}).GetProperty("revision").GetInt32();
void Action(object s,string kind,string mode,string id="")=>Request(s,new{op="extraSet",kind,mode,id,revision=Revision(s)});
void Edit(object s,string kind,string id,params (string field,string value)[] values)=>Request(s,new{op="extraSet",kind,id,edits=values.Select(x=>new{x.field,x.value}),revision=Revision(s)});
string Value(object s,string kind,string id,string field)=>Request(s,new{op="extraEntry",kind,id}).GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==field).GetProperty("value").GetString()!;
void Assert(bool test,string message){checks++;if(!test)throw new Exception(message);}
void Reject(Action action,string message){try{action();}catch(Exception e)when(e.Message!=message){checks++;return;}throw new Exception(message);}
var entityField=type.GetField("entity",BindingFlags.Instance|BindingFlags.NonPublic)!;
PKM Entity(object s)=>(PKM)entityField.GetValue(s)!;
var session=Session(BlankSaveFile.Get(GameVersion.SL,"Fixture"));
var games=Request(session,new{op="sampleGames"}).EnumerateArray().ToArray();Assert(games.Length==39,"39 sample choices");
foreach(var game in games){string code=game.GetProperty("value").GetString()!;var state=Request(session,new{op="demo",version=code});Assert(state.GetProperty("demo").GetBoolean()&&state.GetProperty("hasSave").GetBoolean(),code+" sample loaded");Assert(state.GetProperty("gameVersion").GetString()==code,code+" save identity");var pk=Entity(session);var bases=state.GetProperty("baseStats").EnumerateArray().Select(x=>x.GetInt32()).ToArray();Assert(bases.SequenceEqual(Enumerable.Range(0,6).Select(i=>pk.PersonalInfo.GetBaseStatValue(i))),code+" base stats");Assert(state.GetProperty("potential").GetInt32()==pk.PotentialRating,code+" potential stars");Console.WriteLine("PASS "+code+" sample, identity, base stats and potential");}
foreach(var game in new[]{GameVersion.HG,GameVersion.B2,GameVersion.X,GameVersion.GP,GameVersion.SW,GameVersion.PLA,GameVersion.SL,GameVersion.ZA}) {
 Request(session,new{op="demo",version=game.ToString()});var pk=Entity(session);var before=pk.Data.ToArray();
 if(pk is IScaledSize){Request(session,new{op="cosmeticSet",mode="size",field="HeightScalar",value="200",automatic=true});var edited=Entity(session);Assert(((IScaledSize)edited).HeightScalar==200,"Height update");if(edited is PA8 pa)Assert(pa.Scale==200,"PLA linked scale");if(edited is IScaledSizeValue sv){Assert(sv.HeightAbsolute==sv.CalcHeightAbsolute&&sv.WeightAbsolute==sv.CalcWeightAbsolute,"Calculated measurements");}Request(session,new{op="undo"});Assert(Entity(session).Data.SequenceEqual(before),"Size undo exact");Request(session,new{op="redo"});before=Entity(session).Data.ToArray();Reject(()=>Request(session,new{op="cosmeticSet",mode="size",field="HeightScalar",value="256",automatic=true}),"Overflow accepted");Assert(Entity(session).Data.SequenceEqual(before),"Size rollback");}
 if(pk is G4PKM){Request(session,new{op="cosmeticSet",mode="leaves",value=63});Assert(((G4PKM)Entity(session)).ShinyLeaf==63,"Leaves and crown");before=Entity(session).Data.ToArray();Reject(()=>Request(session,new{op="cosmeticSet",mode="leaves",value=32}),"Crown without leaves accepted");Assert(Entity(session).Data.SequenceEqual(before),"Leaf rollback");Request(session,new{op="undo"});Assert(((G4PKM)Entity(session)).ShinyLeaf==0,"Leaf undo");}
 if(pk is IContestStats){Request(session,new{op="cosmeticSet",mode="contest",all=true});var c=(IContestStats)Entity(session);Assert(c.ContestCool==255&&c.ContestSheen==255,"Contest max");Request(session,new{op="undo"});Assert(((IContestStats)Entity(session)).ContestCool==0,"Contest undo");}
 Request(session,new{op="entityAction",action="maxIV"});var state=Request(session,new{op="state"});Assert(state.GetProperty("potential").GetInt32()==3,"Four star IV potential");
 var data=new byte[Entity(session).SIZE_PARTY];Entity(session).WriteDecryptedDataParty(data);var copy=EntityFormat.GetFromBytes(data,EntityContext.None)!;Assert(copy.Species==25&&copy.ChecksumValid,"Cosmetics Pokémon readback");Assert(copy.PersonalInfo.HP==Entity(session).PersonalInfo.HP,"Base data immutable");
}
Request(session,new{op="demo",version="B2"});Reject(()=>Request(session,new{op="cosmeticSet",mode="leaves",value=63}),"Wrong-format leaves");Reject(()=>Request(session,new{op="cosmeticSet",mode="size",field="Species",value="1",automatic=true}),"Unrelated cosmetic write");
Console.WriteLine($"PASS {checks} presentation and cosmetics requests/assertions");
