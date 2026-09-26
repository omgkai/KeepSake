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
foreach(byte gender in new byte[]{0,1}){
 var save=new SAV4HGSS(new byte[0x80000]){Gender=gender};var s=Session(save);var raw=save.Data.ToArray();
 Request(s,new{op="extraPage",kind="contacts"});Assert(save.Data.SequenceEqual(raw),"Contacts read purity");
 Action(s,"contacts","give");var calls=((SAV4HGSS)Current(s)).GetPokeGearRoloDex().ToArray();Assert(!calls.Contains(gender==0?PokegearNumber.Ethan:PokegearNumber.Lyra)&&!calls.Contains(PokegearNumber.Bike_Shop),"Exclude player and bike shop");
 Edit(s,"contacts","0",("Caller","74"));Assert(Value(s,"contacts","0","Caller")=="74","Contact edit");Request(s,new{op="undo"});Assert(Value(s,"contacts","0","Caller")=="0","Contact Undo");Request(s,new{op="redo"});
 Action(s,"contacts","nontrainers");Assert(((SAV4HGSS)Current(s)).GetPokeGearRoloDex().ToArray().Count(x=>x!=PokegearNumber.None)==9,"Non-trainer contacts");Action(s,"contacts","clear");Assert(((SAV4HGSS)Current(s)).GetPokeGearRoloDex().ToArray().All(x=>x==PokegearNumber.None),"Clear contacts");
 Reject(()=>Edit(s,"contacts","0",("Caller","75")),"Invalid contact accepted");
 var copy=new SAV4HGSS(Current(s).Write().ToArray());Assert(copy.ChecksumsValid,"HGSS checksum");Assert(copy.GetPokeGearRoloDex().ToArray().All(x=>x==PokegearNumber.None),"Contacts reparse");
}
foreach(var game in new[]{GameVersion.BD,GameVersion.SP}){
 var save=(SAV8BS)BlankSaveFile.Get(game,"Fixture");var s=Session(save);var raw=save.Data.ToArray();var page=Request(s,new{op="extraPage",kind="underground8"});Assert(save.Data.SequenceEqual(raw),"Underground read purity");
 string id=page.GetProperty("entries")[0].GetProperty("id").GetString()!;
 Edit(s,"underground8",id,("Count","10"),("New","false"),("Favorite","true"));Assert(Value(s,"underground8",id,"Favorite")=="true","Underground flags");Request(s,new{op="undo"});Assert(Value(s,"underground8",id,"Count")=="0","Underground undo");Request(s,new{op="redo"});
 Action(s,"underground8","give");Assert(((SAV8BS)Current(s)).Underground.ReadItems().Where(i=>i.Type!=UgItemType.None).All(i=>i.Count==i.MaxValue),"Give max per type");
 var expected=Request(s,new{op="extraPage",kind="underground8"}).GetProperty("entries").ToString();var copy=new SAV8BS(Current(s).Write().ToArray());Assert(copy.ChecksumsValid,"BDSP checksums");saveField.SetValue(s,copy);Assert(Request(s,new{op="extraPage",kind="underground8"}).GetProperty("entries").ToString()==expected,"Underground serialized");
 Action(s,"underground8","clear");Assert(((SAV8BS)Current(s)).Underground.ReadItems().Where(i=>i.Type!=UgItemType.None).All(i=>i.Count==0&&!i.IsFavoriteFlag&&!i.HideNewFlag),"Empty semantics");Reject(()=>Edit(s,"underground8",id,("Count","999999999")),"Invalid quantity accepted");
}
SaveFile Normalize(SaveFile save){
 var blocks=save is SAV8SWSH sw?sw.AllBlocks:((SAV9SV)save).AllBlocks;
 var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
 var normalized=blocks.Select(b=>(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type!=SCTypeCode.None?b.Type:b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object,new Memory<byte>(b.Data.ToArray())})).ToArray();
 return (SaveFile)Activator.CreateInstance(save.GetType(),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{normalized},null)!;
}
foreach(var game in new[]{GameVersion.SW,GameVersion.SH,GameVersion.SL,GameVersion.VL}){
 var save=Normalize(BlankSaveFile.Get(game,"Fixture"));var s=Session(save);string kind=save is SAV8SWSH?"raids8":"raids9";
 var before=save.Write().ToArray();var page=Request(s,new{op="extraPage",kind});Assert(save.Write().Span.SequenceEqual(before),"Raid read purity");
 var rows=page.GetProperty("entries").EnumerateArray().ToArray();
 string[] regions=save is SAV8SWSH?["galar","armor","crown"]:["paldea","kitakami","blueberry"];
 foreach(var region in regions){string id=region+":0";Assert(rows.Any(r=>r.GetProperty("id").GetString()==id),"Region available");
   Edit(s,kind,id,("Seed",save is SAV8SWSH?"FEDCBA9876543210":"FEDCBA98"));Assert(Value(s,kind,id,"Seed")== (save is SAV8SWSH?"FEDCBA9876543210":"FEDCBA98"),"Seed exact bits");Request(s,new{op="undo"});Assert(Value(s,kind,id,"Seed")==new string('0',save is SAV8SWSH?16:8),"Seed undo");Request(s,new{op="redo"});
   Reject(()=>Edit(s,kind,id,("Seed","xyz")),"Bad hex accepted");Reject(()=>Edit(s,kind,id,("Seed",new string('F',17))),"Long seed accepted");
   if(save is SAV8SWSH){Edit(s,kind,id,("Hash","FFFFFFFFFFFFFFFF"),("DenType","2"),("Stars","4"),("RandRoll","100"));Edit(s,kind,id,("IsWishingPiece","true"));Assert(Value(s,kind,id,"DenType")=="4","Derived den flags");Reject(()=>Edit(s,kind,id,("DenType","1"),("IsRare","true")),"Conflicting flags accepted");}
   else{
      Edit(s,kind,id,("IsEnabled","true"),("AreaID","1"),("Content","3"),("IsClaimedLeaguePoints","true"));Edit(s,kind,region+":1",("AreaID","2"),("Seed","12345678"));
      Action(s,kind,"copy",id);Assert(Value(s,kind,region+":1","Content")=="3"&&Value(s,kind,region+":1","Seed")=="12345678","Propagate without seed");Action(s,kind,"copyseed",id);Assert(Value(s,kind,region+":1","Seed")=="FEDCBA98","Propagate seed");Assert(Value(s,kind,region+":2","IsEnabled")=="false","Empty position not propagated");
      if(rows.Any(r=>r.GetProperty("id").GetString()==region+":seeds")){Edit(s,kind,region+":seeds",("CurrentSeed","FFFFFFFFFFFFFFFF"),("TomorrowSeed","0123456789ABCDEF"));Assert(Value(s,kind,region+":seeds","CurrentSeed")=="FFFFFFFFFFFFFFFF","64-bit daily seeds");}
   }
 }
 if(save is SAV9SV){Edit(s,"sevenstar","0",("Identifier","20260924"),("Captured","true"),("Defeated","true"));Assert(Value(s,"sevenstar","0","Captured")=="true","Seven-star captured");}
 int rev=Revision(s);Reject(()=>Request(s,new{op="extraSet",kind,id=regions[0]+":0",revision=rev-1,edits=new[]{new{field="Seed",value="1"}}}),"Stale raid edit");
 string selected=regions[0]+":0";var expected=Request(s,new{op="extraEntry",kind,id=selected}).ToString();var active=Current(s);SaveFile copy=active is SAV8SWSH?new SAV8SWSH(active.Write().ToArray()):new SAV9SV(active.Write().ToArray());Assert(copy.ChecksumsValid,"Raid export checksum");saveField.SetValue(s,copy);Assert(Request(s,new{op="extraEntry",kind,id=selected}).ToString()==expected,"Raid export/reparse");
 if(copy is SAV9SV)Assert(Value(s,"sevenstar","0","Identifier")=="20260924","Seven star reparse");Console.WriteLine("PASS "+game+" raid regions, seeds, edit validation, undo and export/reparse");
}
Console.WriteLine($"PASS {checks} contacts, Underground and raid checks; generated fixtures only");
