using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var field=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;
foreach(var game in new[]{GameVersion.RD,GameVersion.C,GameVersion.E,GameVersion.FR,GameVersion.Pt,GameVersion.HG,GameVersion.B2,GameVersion.X,GameVersion.AS,GameVersion.SW,GameVersion.BD,GameVersion.SL,GameVersion.VL}) {
 var save=BlankSaveFile.Get(game,"Test");
 switch(save){
  case SAV1 s:s.Badges=5;break;case SAV2 s:s.Badges=5;break;case SAV3 s:s.Badges=5;break;case SAV4HGSS s:s.Badges=5;s.Badges16=0;break;case SAV4 s:s.Badges=5;break;case SAV5 s:s.Misc.Badges=5;break;case SAV6 s:s.Badges=5;break;case SAV8SWSH s:s.Badges=5;break;case SAV8BS s:s.FlagWork.SetSystemFlag(124,true);s.FlagWork.SetSystemFlag(126,true);break;
  case SAV9SV s:
   var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
   var extra=new uint[]{0x8485AC3A,0x815F4601}.Select(k=>(SCBlock)ctor.Invoke(new object[]{k,SCTypeCode.Bool2,Memory<byte>.Empty}));
   var blocks=s.AllBlocks.Concat(extra).OrderBy(b=>b.Key).ToArray();
   save=(SAV9SV)Activator.CreateInstance(typeof(SAV9SV),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{blocks},null)!;break;
 }
 var session=Activator.CreateInstance(type,new object[]{null!})!;field.SetValue(session,save);
 var rows=JsonSerializer.SerializeToElement(type.GetMethod("TrainerBadges",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null));
 if(rows.EnumerateArray().Count(b=>b.GetProperty("earned").GetBoolean())!=2 || !rows[0].GetProperty("earned").GetBoolean() || rows[1].GetProperty("earned").GetBoolean() || !rows[2].GetProperty("earned").GetBoolean()) throw new Exception("Badge bit mapping "+game);
 if(rows.GetArrayLength()!=(game is GameVersion.C or GameVersion.HG ? 16:game is GameVersion.SL or GameVersion.VL ? 18:8))throw new Exception("Badge count "+game);
 Console.WriteLine("PASS "+game+" earned/missing badge mapping and names");
}

foreach(var game in new[]{GameVersion.SN,GameVersion.US,GameVersion.PLA}) {
 var save=BlankSaveFile.Get(game,"Test");
 if(save is SAV7 s)s.Misc.Stamps=5;
 if(save is SAV8LA la){var block=la.Blocks.GetBlock(SaveBlockAccessor8LA.KExpeditionTeamRank);block.ChangeStoredType(SCTypeCode.UInt32);block.SetValue((uint)3);}
 var session=Activator.CreateInstance(type,new object[]{null!})!;field.SetValue(session,save);
 var rows=JsonSerializer.SerializeToElement(type.GetMethod("TrainerBadges",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null));
 if(rows.GetArrayLength()!=(game==GameVersion.PLA?10:15)||rows.EnumerateArray().Count(b=>b.GetProperty("earned").GetBoolean())!=(game==GameVersion.PLA?3:2))throw new Exception("Progress mapping "+game);
 Console.WriteLine("PASS "+game+" passport stamps / survey rank");
}
{
 uint[] keys=[0x89306FE6,0xB4C3AFE6,0x8205ECAD,0xA803FAAD,0xF90EFD79,0xCDA61DED,0x3B819021,0x46B6CB30,0xEC7361B7,0xA6CDE603,0x9C16DA94,0x0D0602DE,0xBDAC74B3,0x9C6FF7DD,0x6C29ACC5,0xE1271327,0x2A3AC89A,0x71DB2CEB];
 foreach(int mode in new[]{0,1,2,3}) {
  var blank=(SAV9SV)BlankSaveFile.Get(GameVersion.SL,"Test");
  var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
  var receipt=keys.Select((key,i)=>{int value=mode==0?i:mode==1?(i==8?0:-1):mode==2?0:18;var data=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data,value);return (SCBlock)ctor.Invoke(new object[]{key,SCTypeCode.Int32,new Memory<byte>(data)});});
  var blocks=blank.AllBlocks.Where(b=>!keys.Contains(b.Key)).Concat(receipt).OrderBy(b=>b.Key).ToArray();
  var save=(SAV9SV)Activator.CreateInstance(typeof(SAV9SV),BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{blocks},null)!;
  var session=Activator.CreateInstance(type,new object[]{null!})!;field.SetValue(session,save);
  var rows=JsonSerializer.SerializeToElement(type.GetMethod("TrainerBadges",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null));
  int expected=mode==0?18:mode==1?1:0;
  if(rows.EnumerateArray().Count(b=>b.GetProperty("earned").GetBoolean())!=expected)throw new Exception("SV receipt order mode "+mode);
  if(rows[8].GetProperty("artwork").GetString()!="paldea-titan-dragon"||rows[17].GetProperty("artwork").GetString()!="paldea-star-poison")throw new Exception("SV badge art mapping");
  Console.WriteLine("PASS SV receipt mode "+mode+" earned count "+expected);
  if(mode==1&&args.Length>0 && !Directory.Exists(args[0])){var normalized=save.AllBlocks.Select(b=>(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type==SCTypeCode.None?(b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object):b.Type,new Memory<byte>(b.Data.ToArray())})).ToArray();File.WriteAllBytes(args[0],SwishCrypto.Encrypt(normalized));}

 }
}

// HGSS stores Johto and Kanto in separate bytes. Exercise each bit and both
// full regions independently, using the raw save offsets rather than a setter
// whose name could repeat the adapter's original mistaken interpretation.
foreach(var game in new[]{GameVersion.HG,GameVersion.SS}) {
 foreach(int mask in Enumerable.Range(0,16).Select(i=>1<<i).Concat(new[]{0,0xFF,0xFF00,0xFFFF,0xA55A})) {
  var s=new SAV4HGSS(new byte[0x80000]){Version=game,Magic=SAV4.MAGIC_JAPAN_INTL,OT="Badge QA",Language=2};System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(s.General[^12..],SAV4HGSS.GeneralSize);System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(s.Data[(0x40000+SAV4HGSS.GeneralSize-12)..],SAV4HGSS.GeneralSize);s.General[0x7E]=(byte)mask;s.General[0x83]=(byte)(mask>>8);
  if(mask==0xFFFF&&args.Length>0&&Directory.Exists(args[0]))File.WriteAllBytes(Path.Combine(args[0],game+"-all-badges.sav"),s.Write().ToArray());
  if(SaveUtil.GetSaveFile(s.Write()) is not SAV4HGSS)throw new Exception("HGSS fixture detection");
  var copy=new SAV4HGSS(s.Write().ToArray());if(!copy.ChecksumsValid)throw new Exception("HGSS fixture checksum");
  var before=copy.Data.ToArray();var session=Activator.CreateInstance(type,new object[]{null!})!;field.SetValue(session,copy);
  var rows=JsonSerializer.SerializeToElement(type.GetMethod("TrainerBadges",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null));
  for(int i=0;i<16;i++)if(rows[i].GetProperty("earned").GetBoolean()!=((mask&(1<<i))!=0))throw new Exception($"{game} independent badge bit {i}, mask {mask:X4}");
  if(!before.SequenceEqual(copy.Data.ToArray()))throw new Exception("Badge display changed save");
 }
 Console.WriteLine("PASS "+game+" all 16 independent bits, each region, 16/16 completion, checksummed readback and read purity");
}
{
 uint[] receipts=[0x89306FE6,0xB4C3AFE6,0x8205ECAD,0xA803FAAD,0xF90EFD79,0xCDA61DED,0x3B819021,0x46B6CB30,0xEC7361B7,0xA6CDE603,0x9C16DA94,0x0D0602DE,0xBDAC74B3,0x9C6FF7DD,0x6C29ACC5,0xE1271327,0x2A3AC89A,0x71DB2CEB];
 uint[] markers=[0x8485AC3A,0x26216312,0x815F4601,0x16452421,0x9457390D,0x1C5C88A5,0xD0249A05,0x0A9299BC];
 var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
 SCBlock Block(uint key,SCTypeCode kind,byte[] data)=>(SCBlock)ctor.Invoke(new object[]{key,kind,new Memory<byte>(data)});
 foreach(var game in new[]{GameVersion.SL,GameVersion.VL})foreach(int mode in new[]{0,1,2,3,4}) {
  var blank=(SAV9SV)BlankSaveFile.Get(game,"Badges");var blocks=blank.AllBlocks.Where(b=>!receipts.Contains(b.Key)&&!markers.Contains(b.Key)).Select(b=>Block(b.Key,b.Type==SCTypeCode.None?(b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object):b.Type,b.Data.ToArray())).ToList();
  for(int i=0;i<18;i++) {int value=mode==0?17-i:mode==1?(i==8?0:-1):mode==2?-1:mode==3?0:18;byte[] data=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(data,value);blocks.Add(Block(receipts[i],SCTypeCode.Int32,data));}
  foreach(uint key in markers)blocks.Add(Block(key,mode==2?SCTypeCode.Bool2:SCTypeCode.Bool1,[]));
  var saved=SwishCrypto.Encrypt(blocks.OrderBy(b=>b.Key).ToArray());if(mode==0&&args.Length>0&&Directory.Exists(args[0]))File.WriteAllBytes(Path.Combine(args[0],game+"-all-badges.sav"),saved);var sav=new SAV9SV(saved);var before=sav.Write().ToArray();
  var session=Activator.CreateInstance(type,new object[]{null!})!;field.SetValue(session,sav);
  var rows=JsonSerializer.SerializeToElement(type.GetMethod("TrainerBadges",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(session,null));
  int expected=mode==0?18:mode==1?1:mode==2?8:0;
  if(rows.EnumerateArray().Count(x=>x.GetProperty("earned").GetBoolean())!=expected)throw new Exception($"{game} receipt vs marker mode {mode}");
  if(!before.SequenceEqual(sav.Write().ToArray()))throw new Exception("SV badge read purity");
  Console.WriteLine($"PASS {game} receipt/marker mode {mode}, {expected}/18, encrypted readback and read purity");
 }
}
