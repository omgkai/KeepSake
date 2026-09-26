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
SaveFile Reparse(SaveFile save,byte[] bytes)=>save switch{SAV1=>new SAV1(bytes,LanguageID.English,GameVersion.RD),SAV3E=>new SAV3E(bytes),SAV4Pt=>new SAV4Pt(bytes),SAV4HGSS=>new SAV4HGSS(bytes),SAV5B2W2=>new SAV5B2W2(bytes),SAV6XY=>new SAV6XY(bytes),SAV6AO=>new SAV6AO(bytes),SAV7SM=>new SAV7SM(bytes),SAV7USUM=>new SAV7USUM(bytes),_=>throw new Exception("Unsupported test fixture")};
SAV3E EmeraldFixture(){var data=new byte[0x20000];for(int i=0;i<28;i++){System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i*0x1000+0xFF4),(short)(i%14));System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i*0x1000+0xFF8),0x08012025);if(i%14==0)data.AsSpan(i*0x1000,8).Fill(255);}return new SAV3E(data){Language=2};}
foreach(var game in new[]{GameVersion.RD,GameVersion.E,GameVersion.X,GameVersion.AS,GameVersion.SN,GameVersion.US}) {
 SaveFile save=game==GameVersion.E?EmeraldFixture():BlankSaveFile.Get(game,"Fixture");
 if(save is SAV6 or SAV7)System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(save.Data[^0x1F0..],0x42454546);
 var s=Session(save);var raw=save.Data.ToArray();var page=Request(s,new{op="extraPage",kind="fame"});Assert(save.Data.SequenceEqual(raw),game+" read purity");
 string id=save is SAV7?"0":"0:0";var old=Value(s,"fame",id,"Species");
 Edit(s,"fame",id,("Species","25"));Assert(Value(s,"fame",id,"Species")=="25",game+" species edit");
 Request(s,new{op="undo"});Assert(Value(s,"fame",id,"Species")==old,game+" undo");Request(s,new{op="redo"});
 if(save is not SAV7){Edit(s,"fame",id,("Nickname","Spark"),("Level","42"));Assert(Value(s,"fame",id,"Nickname")=="Spark",game+" nickname");Reject(()=>Edit(s,"fame",id,("Nickname","This nickname is much too long")),"Accepted long nickname");Reject(()=>Edit(s,"fame",id,("Level",game==GameVersion.RD?"256":"101")),"Accepted invalid level");}
 if(save is SAV6){Edit(s,"fame","team:0",("Date","2026-09-24"),("HasData","true"),("ClearIndex","7"));Edit(s,"fame",id,("IsShiny","true"),("Move1","85"),("TID16","12345"));Reject(()=>Action(s,"fame","delete","team:0"),"Deleted protected first entry");}
 if(save is SAV7USUM)Edit(s,"fame","starter",("StarterEncryptionConstant","4294967295"));
 int rev=Revision(s);Reject(()=>Request(s,new{op="extraSet",kind="fame",id,revision=rev-1,edits=new[]{new{field="Species",value="26"}}}),"Accepted stale edit");
 var before=Request(s,new{op="extraEntry",kind="fame",id}).ToString();var active=Current(s);var copy=Reparse(active,active.Write().ToArray());Assert(copy.ChecksumsValid,game+" checksums");saveField.SetValue(s,copy);Assert(Request(s,new{op="extraEntry",kind="fame",id}).ToString()==before,game+" serialized fame readback");
 if(copy is SAV1 one){one.SetPartySlotAtIndex(new PK1{Species=25,CurrentLevel=50,Nickname="PIKACHU"},0);Action(s,"fame","register");Assert(((SAV1)Current(s)).HallOfFame.GetEntity(0,0).Species==25,"RD party registration");Action(s,"fame","clearall");Assert(((SAV1)Current(s)).HallOfFameCount==0,"RD clear all");Request(s,new{op="undo"});}
 if(copy is SAV3 three){three.SetPartySlotAtIndex(new PK3{Species=25,CurrentLevel=50,Nickname="Spark",Language=2},0);Action(s,"fame","party","team:2");Assert(HallFame3Entry.GetEntries((SAV3)Current(s))[2].GetMember(0).Species==25,"Gen3 party import");Action(s,"fame","partyall");Assert(HallFame3Entry.GetEntries((SAV3)Current(s)).All(t=>t.GetMember(0).Species==25),"Gen3 party all");}
 if(copy is SAV6){Edit(s,"fame","2:0",("Species","133"));Action(s,"fame","delete","team:1");Assert(Value(s,"fame","1:0","Species")=="133","Gen6 delete compaction");Request(s,new{op="undo"});Assert(Value(s,"fame","2:0","Species")=="133","Gen6 delete undo");}
 // A failed multi-field edit must not retain the earlier change.
 var bytes=Current(s).Write().ToArray();Reject(()=>Edit(s,"fame",id,("Species","99999")),"Accepted unavailable species");Assert(Current(s).Write().Span.SequenceEqual(bytes),game+" rollback");
 Console.WriteLine("PASS "+game+" Hall of Fame edits, bounds, Undo/Redo, revision and serialized checksums");
}
foreach(SaveFile save in new SaveFile[]{new SAV4Pt(new byte[0x80000]),new SAV4HGSS(new byte[0x80000]),BlankSaveFile.Get(GameVersion.B2,"Fixture")}) {
 string kind=save is SAV4?"geonet":"unity";var s=Session(save);var raw=save.Write().ToArray();var page=Request(s,new{op="extraPage",kind});Assert(save.Write().Span.SequenceEqual(raw),"World read purity");
 var place=page.GetProperty("entries").EnumerateArray().First(e=>e.GetProperty("id").GetString()!.StartsWith("place:"));string id=place.GetProperty("id").GetString()!;var old=Value(s,kind,id,"Point");
 Edit(s,kind,id,("Point","2"));Assert(Value(s,kind,id,"Point")=="2","World point");Request(s,new{op="undo"});Assert(Value(s,kind,id,"Point")==old,"World undo");
 Action(s,kind,"legal");var expected=Request(s,new{op="extraPage",kind}).GetProperty("entries").ToString();var active=Current(s);var copy=Reparse(active,active.Write().ToArray());Assert(copy.ChecksumsValid,"World checksums");saveField.SetValue(s,copy);Assert(Request(s,new{op="extraPage",kind}).GetProperty("entries").ToString()==expected,"World serialized");
 if(kind=="unity"){Edit(s,kind,"floor:1",("Unlocked","false"));Assert(!((SAV5)Current(s)).UnityTower.GetUnityTowerFloor(1),"Floor edit");}
 Action(s,kind,"give");Action(s,kind,"clear");Reject(()=>Edit(s,kind,id,("Point","4")),"Accepted invalid marker");Console.WriteLine("PASS "+save.Version+" "+kind+" locations, bulk actions, Undo and serialized checksums");
}
{
 var save=(SAV6XY)BlankSaveFile.Get(GameVersion.X,"Fixture");var s=Session(save);var raw=save.Data.ToArray();var page=Request(s,new{op="extraPage",kind="berries"});Assert(page.GetProperty("entries").GetArrayLength()==32,"Berry plot count");Assert(save.Data.SequenceEqual(raw),"Berry purity");Reject(()=>Edit(s,"berries","0",("0","42")),"Edited readonly berry field");
 Console.WriteLine("PASS XY berry viewer 32 plots and write rejection");
}
Console.WriteLine($"PASS {checks} parity adapter checks; generated fixtures only");
