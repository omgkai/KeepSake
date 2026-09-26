using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;
int checks=0;
object Session(SaveFile save){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,save);return s;}
SAV4Sinnoh Current(object s)=>(SAV4Sinnoh)saveField.GetValue(s)!;
JsonElement Req(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw();throw;}}
int Rev(object s)=>Req(s,new{op="state"}).GetProperty("revision").GetInt32();
void Edit(object s,string id,params (string field,string value)[] values)=>Req(s,new{op="extraSet",kind="underground4",id,revision=Rev(s),edits=values.Select(v=>new{v.field,v.value})});
void Act(object s,string id,string mode)=>Req(s,new{op="extraSet",kind="underground4",id,mode,revision=Rev(s)});
JsonElement Row(object s,string id)=>Req(s,new{op="extraEntry",kind="underground4",id});
void Assert(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
void Reject(Action call,string why){try{call();}catch{checks++;return;}throw new Exception("Expected rejection: "+why);}
byte[] Pouch(object session,string key){var s=Current(session);return key switch{"goods"=>s.GetUGI_Goods().ToArray(),"spheres"=>s.GetUGI_Spheres().ToArray(),"traps"=>s.GetUGI_Traps().ToArray(),_=>s.GetUGI_Treasures().ToArray()};}
foreach(var game in new[]{GameVersion.D,GameVersion.P,GameVersion.Pt}) {
 var session=Session(game==GameVersion.Pt ? new SAV4Pt(new byte[0x80000]) : new SAV4DP(new byte[0x80000]));var before=Current(session).Data.ToArray();
 var page=Req(session,new{op="extraPage",kind="underground4"});Assert(page.GetProperty("entries").GetArrayLength()==165,"All records, pouches and 160 slots");Assert(before.SequenceEqual(Current(session).Data.ToArray()),"Read purity");
 foreach(string key in new[]{"goods","spheres","traps","treasures"}) {
  var field=Row(session,key+":0").GetProperty("fields")[0];var choices=field.GetProperty("choices").EnumerateArray().Where(c=>c.GetProperty("value").GetString()!="0").Select(c=>c.GetProperty("value").GetString()!).ToArray();
  for(int i=0;i<40;i++)Edit(session,key+":"+i,("Item",choices[i%choices.Length]));
  Assert(Pouch(session,key).Take(40).All(x=>x!=0),key+" full capacity");
  var full=Pouch(session,key);Edit(session,key+":7",("Item","0"));Assert(Pouch(session,key)[7]==full[8]&&Pouch(session,key)[39]==0,"Remove compacts "+key);Req(session,new{op="undo"});Assert(Pouch(session,key).SequenceEqual(full),"Undo "+key);Req(session,new{op="redo"});
  before=Current(session).Data.ToArray();Reject(()=>Edit(session,key+":0",("Item","256")),"Invalid item");Reject(()=>Edit(session,key+":40",("Item","1")),"Out of range slot");Reject(()=>Edit(session,key+":0",("Unknown","1")),"Unknown field");Assert(before.SequenceEqual(Current(session).Data.ToArray()),"Rejected edit atomicity");
  var actions=Row(session,"pouch:"+key).GetProperty("actions").EnumerateArray().Select(x=>x.GetProperty("value").GetString()).ToArray();
  if(actions.Contains("give")){Act(session,"pouch:"+key,"give");Assert(Pouch(session,key).Take(40).Count(x=>x!=0)==choices.Length,"Give every type "+key);if(key=="spheres")Assert(Pouch(session,key).Skip(40).Take(choices.Length).All(x=>x==99),"Give spheres size");}
  else {Reject(()=>Act(session,"pouch:"+key,"give"),"Cannot overfill goods");}
  Act(session,"pouch:"+key,"clear");Assert(Pouch(session,key).All(x=>x==0),"Clear "+key);
 }
 Edit(session,"spheres:0",("Item","1"),("Size","17"));Edit(session,"spheres:1",("Item","2"),("Size","255"));Edit(session,"spheres:0",("Item","0"));Assert(Pouch(session,"spheres")[0]==2&&Pouch(session,"spheres")[40]==255&&Pouch(session,"spheres")[41]==0,"Sphere item/size stay paired");
 before=Current(session).Data.ToArray();Reject(()=>Edit(session,"spheres:0",("Item","1"),("Size","256")),"Size bounds");Assert(before.SequenceEqual(Current(session).Data.ToArray()),"Size rejection atomicity");
 // Imported unknown IDs survive opening, unrelated edits and packing.
 Current(session).GetUGI_Goods()[3]=255;Act(session,"pouch:goods","compact");Assert(Pouch(session,"goods")[0]==255,"Unknown item preserved");
 var fields=Row(session,"records").GetProperty("fields").EnumerateArray().ToArray();Assert(fields.Length==13,"All Windows scores");
 Edit(session,"records",fields.Select(f=>(f.GetProperty("id").GetString()!,"999999")).ToArray());Assert(Row(session,"records").GetProperty("fields").EnumerateArray().All(f=>f.GetProperty("value").GetString()=="999999"),"Maximum scores");
 before=Current(session).Data.ToArray();Reject(()=>Edit(session,"records",("UG_PeopleMet","1000000")),"Score upper bound");Reject(()=>Edit(session,"records",("UG_PeopleMet","-1")),"Score lower bound");Reject(()=>Req(session,new{op="extraSet",kind="underground4",id="records",revision=Rev(session)-1,edits=new[]{new{field="UG_PeopleMet",value="0"}}}),"Stale revision");Assert(before.SequenceEqual(Current(session).Data.ToArray()),"Score rejection atomicity");
 var expected=Req(session,new{op="extraPage",kind="underground4"}).ToString();var data=Current(session).Write().ToArray();SAV4Sinnoh copy=game==GameVersion.Pt?new SAV4Pt(data):new SAV4DP(data);Assert(copy.ChecksumsValid,"Save checksums");saveField.SetValue(session,copy);Assert(Req(session,new{op="extraPage",kind="underground4"}).ToString()==expected,"All serialized records and pouches");
 Console.WriteLine("PASS "+game+" Underground all pouches, records, full capacity, compaction, pairing, bulk actions, bounds, Undo/Redo, read purity and checksummed save readback");
}
var hg=Session(BlankSaveFile.Get(GameVersion.HG,"Fixture"));Reject(()=>Req(hg,new{op="extraPage",kind="underground4"}),"HGSS unsupported");

var scratch=Path.Combine(Path.GetTempPath(),"keepsake025-"+Guid.NewGuid());Directory.CreateDirectory(scratch);
void EditKind(object s,string kind,string id,params (string field,string value)[] fields)=>Req(s,new{op="extraSet",kind,id,revision=Rev(s),edits=fields.Select(f=>new{f.field,f.value})});
void ActKind(object s,string kind,string id,string mode)=>Req(s,new{op="extraSet",kind,id,mode,revision=Rev(s)});
SaveFile Save(object s)=>(SaveFile)saveField.GetValue(s)!;
IChatter Chat(object s)=>Save(s) is SAV4 four?four.Chatter:((SAV5)Save(s)).Chatter;
foreach(var game in new[]{GameVersion.D,GameVersion.P,GameVersion.Pt,GameVersion.HG,GameVersion.SS,GameVersion.B,GameVersion.W,GameVersion.B2,GameVersion.W2}) {
 SaveFile sav=game switch{GameVersion.D or GameVersion.P=>new SAV4DP(new byte[0x80000]),GameVersion.Pt=>new SAV4Pt(new byte[0x80000]),GameVersion.HG or GameVersion.SS=>new SAV4HGSS(new byte[0x80000]){Version=game},_=>BlankSaveFile.Get(game,"Voice")};
 var s=Session(sav);var bytes=Enumerable.Range(0,1000).Select(i=>(byte)(i%256)).ToArray();string pcm=Path.Combine(scratch,game+".pcm");File.WriteAllBytes(pcm,bytes);
 Req(s,new{op="extraSet",kind="chatter",id="recording",mode="import",path=pcm,revision=Rev(s)});Assert(Chat(s).Initialized&&Chat(s).Recording.SequenceEqual(bytes),"PCM import");
 var before=Save(s).Data.ToArray();var audio=Convert.FromBase64String(Req(s,new{op="chatterAudio"}).GetProperty("wav").GetString()!);Assert(audio.Length==2044,"Wave length");Assert(System.Text.Encoding.ASCII.GetString(audio,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(audio,8,8)=="WAVEfmt ","Wave header");Assert(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(audio.AsSpan(24))==2000,"Sample rate");for(int i=0;i<1000;i++)Assert(audio[44+i*2]==((bytes[i]&15)<<4)&&audio[45+i*2]==(bytes[i]&240),"Low nibble first");
 Assert(before.SequenceEqual(Save(s).Data.ToArray()),"Audio read purity");
 foreach(string format in new[]{"pcm","wav"}){string path=Path.Combine(scratch,game+"-out."+format);Req(s,new{op="extraExport",kind="chatter",id="recording",format,path});Assert(File.ReadAllBytes(path).SequenceEqual(format=="wav"?audio:bytes),"Audio export");}
 string bad=Path.Combine(scratch,"bad.pcm");File.WriteAllBytes(bad,new byte[999]);Reject(()=>Req(s,new{op="extraSet",kind="chatter",id="recording",mode="import",path=bad,revision=Rev(s)}),"Incorrect PCM length");Assert(Chat(s).Recording.SequenceEqual(bytes),"Import atomicity");Reject(()=>EditKind(s,"chatter","recording",("Chance","100")),"Derived confusion field");
 EditKind(s,"chatter","recording",("Initialized","false"));Assert(!Chat(s).Initialized&&Chat(s).Recording.SequenceEqual(bytes),"Disable preserves recording");Req(s,new{op="undo"});Assert(Chat(s).Initialized,"Undo voice enable");ActKind(s,"chatter","recording","clear");Assert(!Chat(s).Initialized&&Chat(s).Recording.ToArray().All(x=>x==0),"Clear voice");Req(s,new{op="undo"});Assert(Chat(s).Recording.SequenceEqual(bytes),"Undo clear");
 var written=Save(s).Write().ToArray();SaveFile copy=game switch{GameVersion.D or GameVersion.P=>new SAV4DP(written),GameVersion.Pt=>new SAV4Pt(written),GameVersion.HG or GameVersion.SS=>new SAV4HGSS(written),GameVersion.B or GameVersion.W=>new SAV5BW(written),_=>new SAV5B2W2(written)};Assert(copy.ChecksumsValid,"Voice save checksum");saveField.SetValue(s,copy);Assert(Chat(s).Initialized&&Chat(s).Recording.SequenceEqual(bytes),"Voice serialized roundtrip");
 Console.WriteLine("PASS "+game+" Chatter PCM/WAV, all 2,000 samples, enable/clear, rejected imports, Undo and serialized save readback");
}
foreach(var game in new[]{GameVersion.X,GameVersion.Y,GameVersion.OR,GameVersion.AS}) {
 var s=Session(BlankSaveFile.Get(game,"Training"));SuperTrainBlock Train()=>((ISaveBlock6Main)Save(s)).SuperTrain;
 var before=Save(s).Data.ToArray();var page=Req(s,new{op="extraPage",kind="training6"});Assert(page.GetProperty("entries").GetArrayLength()==51,"32 stages, 12 bags, bag summary and 6 distribution flags");Assert(before.SequenceEqual(Save(s).Data.ToArray()),"Training read purity");
 for(int i=0;i<32;i++){EditKind(s,"training6","stage:"+i,("Time1","12.375"),("Time2","2.5e1"),("Species1","25"),("Species2","133"),("Form1","1"),("Gender1","1"),("Form2","2"),("Gender2","0"),("Unlocked","true"));Assert(Train().GetTime1(i)==12.375f&&Train().GetTime2(i)==25f&&Train().GetHolder1(i).Species==25&&Train().GetHolder2(i).Form==2,"Both fractional record holders");}
 ActKind(s,"training6","stage:31","clearRecords");Assert(Train().GetHolder1(31).Species==0&&Train().GetTime2(31)==0&&Train().GetIsRegimenUnlocked(31),"Clear preserves unlock");Req(s,new{op="undo"});Assert(Train().GetHolder1(31).Species==25,"Record Undo");
 for(int i=0;i<12;i++)EditKind(s,"training6","bag:"+i,("Bag","1"));EditKind(s,"training6","bag:4",("Bag","0"));Assert(Train().GetBag(4)==1&&Train().GetBag(11)==0,"Pack bags and clear tail");ActKind(s,"training6","bags","clearBags");Assert(Enumerable.Range(0,12).All(i=>Train().GetBag(i)==0),"Clear bags");Req(s,new{op="undo"});
 ActKind(s,"training6","","unlockDistribution");Assert(Enumerable.Range(0,6).All(i=>Train().GetIsDistributionUnlocked(i)),"Distribution unlock");EditKind(s,"training6","distribution:5",("Unlocked","false"));Assert(!Train().GetIsDistributionUnlocked(5),"Individual distribution flag");
 foreach(string invalid in new[]{"NaN","Infinity","-1","1e100","text"}){before=Save(s).Data.ToArray();Reject(()=>EditKind(s,"training6","stage:0",("Species1","1"),("Time1",invalid)),"Invalid float "+invalid);Assert(before.SequenceEqual(Save(s).Data.ToArray()),"Training rejected atomically");}
 Reject(()=>EditKind(s,"training6","stage:32",("Time1","1")),"Unreleased stage");Reject(()=>EditKind(s,"training6","bag:12",("Bag","1")),"Bag bounds");Reject(()=>EditKind(s,"training6","stage:0",("Form1","256")),"Form byte bounds");
 var expected=Req(s,new{op="extraPage",kind="training6"}).ToString();var data=Save(s).Write().ToArray();SaveFile copy=game is GameVersion.X or GameVersion.Y?new SAV6XY(data):new SAV6AO(data);Assert(copy.ChecksumsValid,"Training save checksum");saveField.SetValue(s,copy);Assert(Req(s,new{op="extraPage",kind="training6"}).ToString()==expected,"Training serialized roundtrip");
 Console.WriteLine("PASS "+game+" Super Training all stages/holders/bags, decimal seconds, unlocks, bounds, Undo and serialized save readback");
}
Directory.Delete(scratch,true);
Console.WriteLine($"PASS {checks} requests/assertions");
