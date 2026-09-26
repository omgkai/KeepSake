using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;int checks=0;
object Session(SaveFile save){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,save);return s;}
JsonElement Request(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
SAV5B2W2 Current(object s)=>(SAV5B2W2)saveField.GetValue(s)!;
int Revision(object s)=>Request(s,new{op="state"}).GetProperty("revision").GetInt32();
void Action(object s,string mode,string id="",string path="")=>Request(s,new{op="extraSet",kind="avenue",mode,id,path,revision=Revision(s)});
void Edit(object s,string id,params (string field,string value)[] values)=>Request(s,new{op="extraSet",kind="avenue",id,edits=values.Select(x=>new{x.field,x.value}),revision=Revision(s)});
JsonElement Entry(object s,string id)=>Request(s,new{op="extraEntry",kind="avenue",id});
string Value(object s,string id,string field)=>Entry(s,id).GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==field).GetProperty("value").GetString()!;
void Assert(bool test,string message){checks++;if(!test)throw new Exception(message);}
void Reject(Action action,string message){try{action();}catch(Exception e)when(e.Message!=message){checks++;return;}throw new Exception(message);}
var directory=Path.Combine(Path.GetTempPath(),"KeepSake-Avenue-"+Guid.NewGuid());Directory.CreateDirectory(directory);
try {
foreach(var game in new[]{GameVersion.B2,GameVersion.W2}) {
 var save=(SAV5B2W2)BlankSaveFile.Get(game,"Fixture");var s=Session(save);var raw=save.Data.ToArray();
 var page=Request(s,new{op="extraPage",kind="avenue"});Assert(page.GetProperty("entries").GetArrayLength()==66,"66 records");Assert(save.Data.SequenceEqual(raw),"Read purity");
 foreach(var id in new[]{"self","shop:7","visitor:0","fan:11","assistant:3"}) {
   Edit(s,id,("Name","Kai"),("Language","2"),("Gender","1"),("PlayedHours","1000"),("PlayedMinutes","59"),("IsInteractedToday","true"),("Greeting","Hello"),("Seed","4294967295"));
   Assert(Value(s,id,"Name")=="Kai"&&Value(s,id,"Gender")=="1"&&Value(s,id,"PlayedHours")=="1000",id+" profile changes");
   Request(s,new{op="undo"});Assert(Value(s,id,"Name")!="Kai",id+" undo");Request(s,new{op="redo"});
   Reject(()=>Edit(s,id,("Name","TooLongName")),"Long name accepted");Reject(()=>Edit(s,id,("PlayedHours","1024")),"Packed hours overflow");
 }
 Edit(s,"settings",("Name","Kai’s Avenue"),("PlayerTitle","Shopkeeper"),("Rank","9876"),("CountVisitor","8"),("CountFan","12"),("ScriptFlag","true"),("CeilingColor","1"),("PromotionDaysElapsed","6"),("IsPromotionActive","true"));
 Assert(Current(s).JoinAvenue.Settings.Rank==9876&&Current(s).JoinAvenue.CountVisitor==8&&Current(s).JoinAvenue.CountFan==12&&Current(s).JoinAvenue.ScriptFlag,"Settings and header controls");
 Edit(s,"visitor:0",("ShopType","320"),("DesiredShopType","81"),("ShopRank","10"),("ShopCountCafe","15"),("FavoriteSpecies","25"),("UnknownBits0_8","511"),("UnknownBits21_27","127"),("UnknownBits28_31","15"),("DateAdventureStart","2012-10-07"),("record:7","4294967295"),("trivia:15","255"),("activity:3","250"),("activityDate:3","2026-09-24"));
 var v=Current(s).JoinAvenue.GetVisitor(0);Assert(v.ShopTypeTuple is {Version:3,Type:JoinAvenueShopType5.Cafe,Rank:9},"Named shop decodes correctly");Assert(v.DesiredShopTypeTuple is {Version:1,Type:JoinAvenueShopType5.Raffle,Rank:0},"Desired shop decoded");Assert(v.GetRecord(JoinAvenueRecordIndex5.CountEggHatched)==uint.MaxValue&&v.GetTrivia(15)==255&&v.GetActivityDate(3).Date==new DateOnly(2026,9,24),"Arrays and dates");
 Edit(s,"visitor:0",("raw:Date1","65535"));Assert(Current(s).JoinAvenue.GetVisitor(0).Date1.RawValue==65535,"Raw date preserved");Edit(s,"visitor:0",("Name","Iris"));Assert(Current(s).JoinAvenue.GetVisitor(0).Date1.RawValue==65535,"Invalid raw date untouched by unrelated changes");
 var before=Current(s).Data.ToArray();Reject(()=>Edit(s,"visitor:0",("Date1","2020-01-01"),("raw:Date1","123")),"Date alias conflict accepted");Assert(Current(s).Data.SequenceEqual(before),"Conflict rollback");
 Reject(()=>Edit(s,"visitor:0",("Name","New"),("ShopCountCafe","16")),"Packed count overflow accepted");Assert(Current(s).Data.SequenceEqual(before),"Validation rollback");
 Reject(()=>Edit(s,"visitor:0",("activityDate:3","2025-02-29")),"Invalid date accepted");
 foreach(string invalid in new[]{"visitor:-1","visitor:8","fan:12","assistant:4","shop:8","history:32"})Reject(()=>Edit(s,invalid,("Name","Kai")),"Invalid row accepted");
 Edit(s,"history:31",("TID","12345"),("SID","54321"));Assert(Current(s).JoinAvenue.Settings.GetVisitingPlayerTrainerID(31)==(12345u|(54321u<<16)),"History ID split");
 Action(s,"resetvisits","settings");Assert(Current(s).JoinAvenue.Settings.VisitingPlayerDatabase.ToArray().All(i=>i==uint.MaxValue),"Reset visitor history");Request(s,new{op="undo"});
 foreach(var id in new[]{"visitor:0","fan:11","assistant:3"}) {
   string path=Path.Combine(directory,id.Replace(':','-'));Request(s,new{op="extraExport",kind="avenue",id,path});var bytes=File.ReadAllBytes(path);
   Assert(bytes.Length==(id.StartsWith("visitor")?196:id.StartsWith("fan")?96:88),"Export exact size");
   Edit(s,id,("Name","Other"));Action(s,"import",id,path);Assert(Value(s,id,"Name")!="Other","Same-kind import");
   Action(s,"import","shop:0",path);Assert(Value(s,"shop:0","Name")==Value(s,id,"Name"),"Cross-kind profile import");
 }
 var invalidFile=Path.Combine(directory,"invalid");File.WriteAllBytes(invalidFile,new byte[197]);before=Current(s).Data.ToArray();Reject(()=>Action(s,"import","visitor:0",invalidFile),"Bad file accepted");Assert(Current(s).Data.SequenceEqual(before),"Bad import unchanged");Reject(()=>Action(s,"import","settings",invalidFile),"Settings import accepted");
 int rev=Revision(s);Reject(()=>Request(s,new{op="extraSet",kind="avenue",id="settings",revision=rev-1,edits=new[]{new{field="Rank",value="1"}}}),"Stale edit accepted");
 var expected=Entry(s,"visitor:0").ToString();var copy=new SAV5B2W2(Current(s).Write().ToArray());Assert(copy.ChecksumsValid,"Export checksums");saveField.SetValue(s,copy);Assert(Entry(s,"visitor:0").ToString()==expected,"Export/reparse fields");
 if(args.Length>0 && game==GameVersion.B2)File.WriteAllBytes(args[0],copy.Write().ToArray());
 Console.WriteLine("PASS "+game+" Join Avenue profiles, shops, dates, arrays, files, undo, validation and export/reparse");
}
Console.WriteLine($"PASS {checks} Join Avenue checks (synthetic saves)");
}finally{Directory.Delete(directory,true);}
