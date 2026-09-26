using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;int checks=0;
object Session(SaveFile sav){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,sav);return s;}
JsonElement Request(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}
SaveFile Current(object s)=>(SaveFile)saveField.GetValue(s)!;
int Revision(object s)=>Request(s,new{op="state"}).GetProperty("revision").GetInt32();
string Rows(object s,string kind)=>Request(s,new{op="extraPage",kind}).GetProperty("entries").ToString();
void Action(object s,string kind,string mode,string id="")=>Request(s,new{op="extraSet",kind,mode,id,revision=Revision(s)});
void Edit(object s,string kind,string id,string field,string value)=>Request(s,new{op="extraSet",kind,id,edits=new[]{new{field,value}},revision=Revision(s)});
foreach(var game in new[]{GameVersion.RD,GameVersion.C,GameVersion.HG,GameVersion.Pt,GameVersion.B2,GameVersion.X,GameVersion.AS,GameVersion.SN,GameVersion.US,GameVersion.GP,GameVersion.BD}) {
    SaveFile sav=game switch {GameVersion.Pt=>new SAV4Pt(new byte[0x80000]),GameVersion.HG=>new SAV4HGSS(new byte[0x80000]),_=>BlankSaveFile.Get(game,"Fixture")};
    if(sav is SAV6 or SAV7)System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(sav.Data[^0x1F0..],0x42454546);
    if(sav is SAV1 one){var sp=new G1OverworldSpawner(one);foreach(var flag in sp.GetFlagPairs())flag.SetState(true);sp.Save();}
    var session=Session(sav);var tools=Request(session,new{op="extraTools"}).EnumerateArray().ToArray();
    foreach(var tool in tools){string kind=tool.GetProperty("id").GetString()!;if(kind is "fame" or "geonet" or "unity" or "berries" or "avenue" or "contacts" or "underground8" or "raids8" or "raids9" or "sevenstar")continue; // Covered by Parity019Checks, Parity020Checks and JoinAvenueChecks.
        var original=Rows(session,kind);var page=Request(session,new{op="extraPage",kind});
        if(kind=="respawn1"){var row=page.GetProperty("entries")[0];Action(session,kind,"reset",row.GetProperty("id").GetString()!);}
        else if(kind=="gsball")Action(session,kind,"enable","event");
        else if(kind=="roamer6")Edit(session,kind,"roamer","Species","145");
        else if(kind=="honey"){Edit(session,kind,"0","Group","1");Action(session,kind,"ready","0");}
        else if(kind=="captures"){Edit(session,kind,"0","Captured","12");Action(session,kind,"sum","totals");}
        else Action(session,kind,"give");
        var expected=Rows(session,kind);var data=Current(session).Write();SaveFile copy=game switch {GameVersion.Pt=>new SAV4Pt(data),GameVersion.HG=>new SAV4HGSS(data),GameVersion.RD=>new SAV1(data,LanguageID.English,GameVersion.RD),GameVersion.C=>new SAV2(data,LanguageID.English,game),GameVersion.B2=>new SAV5B2W2(data),GameVersion.X=>new SAV6XY(data),GameVersion.AS=>new SAV6AO(data),GameVersion.SN=>new SAV7SM(data),GameVersion.US=>new SAV7USUM(data),GameVersion.GP=>new SAV7b(data),GameVersion.BD=>new SAV8BS(data),_=>throw new Exception("Missing fixture constructor "+game)};
        if(!copy.ChecksumsValid)throw new Exception("Checksum "+game);saveField.SetValue(session,copy);if(Rows(session,kind)!=expected)throw new Exception("Roundtrip "+kind);
        if(kind=="zygarde"&&copy is SAV7USUM us&&us.GetRecord(72)!=us.EventWork.ZygardeCellCount)throw new Exception("Ultra linked record mismatch");
        Console.WriteLine($"PASS {game} {kind} serialized readback and checksums");
    }
}
// Synthetic Hoenn clocks / roaming bytes, including unrelated-byte preservation.
foreach(var game in new[]{GameVersion.R,GameVersion.E,GameVersion.FR}) {
    var sav=(SAV3)BlankSaveFile.Get(game,"Fixture");var session=Session(sav);type.GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,true);byte[] original=sav.LargeBlock.RoamerData.ToArray();Edit(session,"roamer3","roamer","PID","12345678");var changed=((SAV3)Current(session)).LargeBlock.RoamerData.ToArray();for(int i=0;i<original.Length;i++)if((i<4||i>=8)&&original[i]!=changed[i])throw new Exception("Roamer unrelated byte changed");Request(session,new{op="undo"});if(!((SAV3)Current(session)).LargeBlock.RoamerData.Span.SequenceEqual(original))throw new Exception("Roamer undo");
    if(sav.SmallBlock is ISaveBlock3SmallHoenn){Action(session,"clock3","berryfix");if(((ISaveBlock3SmallHoenn)((SAV3)Current(session)).SmallBlock).ClockElapsed.Day!=734)throw new Exception("Berry fix");}
    Console.WriteLine($"PASS {game} roaming-byte preservation and applicable clock fix (in-memory buffers)");
}
{
    var blank=(SAV9ZA)BlankSaveFile.Get(GameVersion.ZA,"Fixture");
    var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
    var blocks=blank.AllBlocks.Select(b=>(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type!=SCTypeCode.None?b.Type:b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object,new Memory<byte>(b.Data.ToArray())})).ToArray();
    var za=new SAV9ZA(SwishCrypto.Encrypt(blocks));var session=Session(za);var before=za.AllBlocks.ToDictionary(b=>b.Key,b=>b.Data.ToArray());
    Edit(session,"donuts","0","MillisecondsSince1970","1780000000000");Edit(session,"donuts","0","Berry1",Request(session,new{op="extraEntry",kind="donuts",id="0"}).GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()=="Berry1").GetProperty("choices")[1].GetProperty("value").GetString()!);Action(session,"donuts","recalculate","0");
    var expected=Rows(session,"donuts");var active=(SAV9ZA)Current(session);var changed=active.AllBlocks.Where(b=>!b.Data.SequenceEqual(before[b.Key])).ToArray();if(changed.Length!=1)throw new Exception("Donut edit changed unrelated blocks");
    var copy=new SAV9ZA(active.Write());if(!copy.ChecksumsValid)throw new Exception("ZA checksum");saveField.SetValue(session,copy);if(Rows(session,"donuts")!=expected)throw new Exception("Donut serialized readback");
    if(args.Length>0)File.WriteAllBytes(args[0],copy.Write().ToArray());
    Console.WriteLine("PASS ZA donut serialized readback, checksums and unrelated-block preservation");
}
Console.WriteLine($"PASS {checks} direct game-extra adapter operations; generated fixtures only");
