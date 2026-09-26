using System.Reflection;
using System.Text.Json;
using System.IO.Compression;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;
int checks=0;
object Session(SaveFile save){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,save);return s;}
SaveFile Save(object s)=>(SaveFile)saveField.GetValue(s)!;
IReadOnlyList<SCBlock> Blocks(object s)=>((ISCBlockArray)Save(s)).AllBlocks;
JsonElement Req(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw();throw;}}
void Assert(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
void Reject(Action a,string why){try{a();}catch{checks++;return;}throw new Exception("Expected rejection: "+why);}
int Rev(object s)=>Req(s,new{op="state"}).GetProperty("revision").GetInt32();
JsonElement Review(object s,string path,string mode="folder")=>Req(s,new{op="saveBlocksReview",path,mode});
void Apply(object s,JsonElement r)=>Req(s,new{op="saveBlocksApply",token=r.GetProperty("token").GetString(),revision=r.GetProperty("revision").GetInt32()});
string Fingerprint(object s)=>string.Join(";",Blocks(s).Select(b=>$"{b.Key}:{b.Type}:{b.SubType}:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b.Data))));
var scratch=Path.Combine(Path.GetTempPath(),"keepsake-block027-"+Guid.NewGuid());Directory.CreateDirectory(scratch);
var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
SaveFile Fixture(GameVersion game) {
 var original=BlankSaveFile.Get(game,"Blocks");
 if(original is SAV8LA la)la.LastSaved.Timestamp=new DateTime(2026,9,26);
 if(original is SAV9SV sv)sv.LastSaved.Timestamp=new DateTime(2026,9,26);
 if(original is SAV9ZA za)za.LastSaved.Timestamp=new DateTime(2026,9,26);
 var data=((ISCBlockArray)original).AllBlocks.Select(b=>(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type!=SCTypeCode.None?b.Type:b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object,new Memory<byte>(b.Data.ToArray())})).ToArray();
 data=data.Concat(new[]{(SCBlock)ctor.Invoke(new object[]{0xF00DBAAAu,SCTypeCode.Bool1,Memory<byte>.Empty})}).OrderBy(b=>b.Key).ToArray();
 var raw=SwishCrypto.Encrypt(data);return game switch{GameVersion.SW=>new SAV8SWSH(raw),GameVersion.PLA=>new SAV8LA(raw),GameVersion.SL=>new SAV9SV(raw),_=>new SAV9ZA(raw)};
}
foreach(var game in new[]{GameVersion.SW,GameVersion.PLA,GameVersion.SL,GameVersion.ZA}) {
 var s=Session(Fixture(game));var folder=Path.Combine(scratch,game.ToString());Directory.CreateDirectory(folder);
 // Select opaque blocks, avoiding trainer/box metadata when the UI refreshes.
 var targets=Blocks(s).Where(b=>b.Data.Length is >=16 and <=128).Take(2).ToArray();Assert(targets.Length==2,"Fixture blocks");
 foreach(var b in targets){var bytes=b.Data.ToArray();bytes[3]^=0x51;File.WriteAllBytes(Path.Combine(folder,$"{b.Key:X8} example.bin"),bytes);}
 var before=Fingerprint(s);var review=Review(s,folder);Assert(review.GetProperty("canApply").GetBoolean()&&review.GetProperty("entries").GetArrayLength()==2,"Two planned changes");Assert(Fingerprint(s)==before,"Preview read purity");
 foreach(var row in review.GetProperty("entries").EnumerateArray())Assert(row.GetProperty("changedBytes").GetInt32()==1&&row.GetProperty("firstOffset").GetInt32()==3,"Byte comparison location");
 Apply(s,review);var after=Fingerprint(s);Assert(before!=after,"Bulk apply");Req(s,new{op="undo"});Assert(Fingerprint(s)==before,"One-step Undo");Req(s,new{op="redo"});Assert(Fingerprint(s)==after,"Redo");Reject(()=>Apply(s,review),"Token reuse");
 var same=Review(s,folder);Assert(!same.GetProperty("canApply").GetBoolean()&&same.GetProperty("unchanged").GetInt32()==2,"Unchanged import no-op");
 Req(s,new{op="undo"});review=Review(s,folder);Req(s,new{op="saveBlockSet",key=targets[0].Key.ToString("X8"),mode="import",path=Path.Combine(folder,$"{targets[0].Key:X8} example.bin"),revision=Rev(s)});Reject(()=>Apply(s,review),"Stale revision");Req(s,new{op="undo"});
 // One invalid member prevents the entire batch from being applied.
 string invalid=Path.Combine(folder,"FFFFFFFF.bin");File.WriteAllBytes(invalid,new byte[1]);before=Fingerprint(s);var bad=Review(s,folder);Assert(!bad.GetProperty("canApply").GetBoolean()&&bad.GetProperty("issues").GetArrayLength()>0,"Unknown block rejected");Reject(()=>Apply(s,bad),"Blocked plan");Assert(Fingerprint(s)==before,"Rejected review preserves blocks");File.Delete(invalid);
 invalid=Path.Combine(folder,$"{targets[0].Key:X8} duplicate.bin");File.WriteAllBytes(invalid,targets[0].Data.ToArray());Assert(Review(s,folder).GetProperty("issues").GetArrayLength()>0,"Duplicate key rejected");File.Delete(invalid);
 invalid=Path.Combine(folder,$"{targets[0].Key:X8} example.bin");var correct=File.ReadAllBytes(invalid);File.WriteAllBytes(invalid,new byte[1]);Assert(!Review(s,folder).GetProperty("canApply").GetBoolean(),"Length mismatch");File.WriteAllBytes(invalid,correct);
 // Roundtrip our archive including boolean block type, which raw folders cannot encode.
 var flag=Blocks(s).First(b=>b.Type==SCTypeCode.Bool1);var zip=Path.Combine(scratch,game+".zip");Req(s,new{op="saveBlocksExport",path=zip});Req(s,new{op="saveBlockSet",key=flag.Key.ToString("X8"),value="true",revision=Rev(s)});
 var archive=Review(s,zip,"archive");Assert(archive.GetProperty("canApply").GetBoolean()&&archive.GetProperty("entries").GetArrayLength()==1,"Archive preserves bool type");Apply(s,archive);Assert(Blocks(s).First(b=>b.Key==flag.Key).Type==SCTypeCode.Bool1,"Boolean restored");
 var written=Save(s).Write().ToArray();bool validHash=SwishCrypto.GetIsHashValid(written);SaveFile reopen=game switch{GameVersion.SW=>new SAV8SWSH(written),GameVersion.PLA=>new SAV8LA(written),GameVersion.SL=>new SAV9SV(written),_=>new SAV9ZA(written)};Assert(reopen.ChecksumsValid && validHash,"Serialized save checksums and encrypted hash");var expected=Fingerprint(s);saveField.SetValue(s,reopen);Assert(Fingerprint(s)==expected,"Serialized block readback");
 for(int options=0;options<16;options++){var path=Path.Combine(scratch,"raw.bin");Req(s,new{op="saveBlocksRawExport",path,options});Assert(File.ReadAllBytes(path).SequenceEqual(SCBlockUtil.ExportAllBlocks(Blocks(s),(SCBlockExportOption)options)),"Raw format "+options);}
 Reject(()=>Req(s,new{op="saveBlocksRawExport",path=Path.Combine(scratch,"bad.bin"),options=16}),"Unknown export option");
 // Fit an otherwise opaque synthetic block to a recognized save length.
 // This exercises file detection and comparison; the fixture is not a playable save.
 var prefix=game switch{GameVersion.SW=>"SIZE_G8SWSH",GameVersion.PLA=>"SIZE_G8LA",GameVersion.SL=>"SIZE_G9_",_=>"SIZE_G9ZA"};
 int target=typeof(SaveUtil).GetFields(BindingFlags.NonPublic|BindingFlags.Static).Where(f=>f.Name.StartsWith(prefix)&&f.FieldType==typeof(int)).Max(f=>(int)f.GetRawConstantValue()!);
 var blocks=Blocks(s);int delta=target-SwishCrypto.Encrypt(blocks).Length;var meta=new SCBlockMetadata(((ISCBlockArray)Save(s)).Accessor,[]);
 var opaque=blocks.Where(b=>b.Type==SCTypeCode.Object&&b.Data.Length+delta>=16&&meta.GetBlockName(b,out _)==null).OrderByDescending(b=>b.Data.Length).First();
 var fit=blocks.Select(b=>b.Key==opaque.Key?(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type,new Memory<byte>(new byte[b.Data.Length+delta])}):b).ToArray();var raw=SwishCrypto.Encrypt(fit);
 Assert(raw.Length==target,"Recognized-size fixture");var other=Path.Combine(scratch,game+".sav");File.WriteAllBytes(other,raw);
 Assert(SaveUtil.TryGetSaveFile(other,out var recognizable),"Comparison fixture detection "+game);
 var compareSession=Session(recognizable!);var diff=Review(compareSession,other,"compare");Assert(diff.GetProperty("comparison").GetBoolean()&&diff.GetProperty("entries").GetArrayLength()==0&&!diff.GetProperty("canApply").GetBoolean(),"Same-save comparison");
 var oldFingerprint=Fingerprint(compareSession);var change=Blocks(compareSession).First(b=>b.Key==targets[0].Key);change.Data[3]^=0x20;
 var beforeCompare=Fingerprint(compareSession);diff=Review(compareSession,other,"compare");Assert(diff.GetProperty("entries").GetArrayLength()==1&&diff.GetProperty("entries")[0].GetProperty("firstOffset").GetInt32()==3,"File comparison reports changed byte");Assert(Fingerprint(compareSession)==beforeCompare,"Comparison never mutates workspace");Reject(()=>Apply(compareSession,diff),"Comparison cannot be imported");
 if(game==GameVersion.SW){var ui=Path.Combine(Directory.GetCurrentDirectory(),"work/tests/027-ui");Directory.CreateDirectory(ui);File.Copy(other,Path.Combine(ui,"compare.sav"),true);File.WriteAllBytes(Path.Combine(ui,"baseline.sav"),Save(compareSession).Write().ToArray());}
 Console.WriteLine("PASS "+game+" native-file comparison, byte offsets and read purity (synthetic opaque-block sizing)");
 Console.WriteLine("PASS "+game+" bulk folder/archive preview, atomic validation, bools, stale plans, Undo/Redo, serialization, all 16 raw formats");
}
// Archive validation doesn't extract paths or tolerate missing/duplicate blocks.
var session=Session(Fixture(GameVersion.SW));var baseZip=Path.Combine(scratch,"base.zip");Req(session,new{op="saveBlocksExport",path=baseZip});
foreach(string mode in new[]{"duplicate","missing","traversal","badtype"}) {
 var zip=Path.Combine(scratch,mode+".zip");File.Copy(baseZip,zip,true);
 using(var z=ZipFile.Open(zip,ZipArchiveMode.Update)) {
  var block=z.Entries.First(e=>e.Name.EndsWith(".bin"));string name=block.Name;
  if(mode=="duplicate")z.CreateEntry(name);
  else if(mode=="missing")block.Delete();
  else if(mode=="traversal")z.CreateEntry("../escape.bin");
  else {var entry=z.GetEntry("blocks.json")!;string text;using(var reader=new StreamReader(entry.Open()))text=reader.ReadToEnd();entry.Delete();using var writer=new StreamWriter(z.CreateEntry("blocks.json").Open());writer.Write(text.Replace("\"Object\"","\"999\""));}
 }
 var before=Fingerprint(session);
 if(mode=="badtype")Reject(()=>Review(session,zip,"archive"),"Unknown stored type");else Assert(!Review(session,zip,"archive").GetProperty("canApply").GetBoolean(),"Bad archive "+mode);
 Assert(Fingerprint(session)==before&&!File.Exists(Path.Combine(scratch,"escape.bin")),"Archive read purity");
}
Directory.Delete(scratch,true);Console.WriteLine($"PASS {checks} requests/assertions");
