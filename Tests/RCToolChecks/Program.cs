using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;
int checks=0;
object NewSession(SaveFile save){var session=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(session,save);return session;}
JsonElement Request(object session,object request){using var json=JsonDocument.Parse(JsonSerializer.Serialize(request));checks++;return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(session,new object[]{json.RootElement})!);}
SaveFile Current(object session)=>(SaveFile)saveField.GetValue(session)!;
int Revision(object session)=>Request(session,new{op="state"}).GetProperty("revision").GetInt32();
foreach(var version in new[]{GameVersion.PLA,GameVersion.SL,GameVersion.ZA}) {
    var save=BlankSaveFile.Get(version,"Fixture");
    // Real stored SC types, deterministic synthetic payloads; not playable saves.
    var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
    var blocks=((ISCBlockArray)save).AllBlocks.Select(b=>(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type!=SCTypeCode.None?b.Type:b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object,new Memory<byte>(b.Data.ToArray())})).ToArray();
    var bytes=SwishCrypto.Encrypt(blocks);
    save=version switch{GameVersion.PLA=>new SAV8LA(bytes),GameVersion.SL=>new SAV9SV(bytes),_=>new SAV9ZA(bytes)};
    if(save is SAV9SV sv){foreach(uint key in new[]{SaveBlockAccessor9SV.KFashionUnlockedEyewear,SaveBlockAccessor9SV.KFashionUnlockedGloves,SaveBlockAccessor9SV.KFashionUnlockedBag,SaveBlockAccessor9SV.KFashionUnlockedFootwear,SaveBlockAccessor9SV.KFashionUnlockedHeadwear,SaveBlockAccessor9SV.KFashionUnlockedLegwear,SaveBlockAccessor9SV.KFashionUnlockedClothing,SaveBlockAccessor9SV.KFashionUnlockedPhoneCase}){var b=sv.Blocks.GetBlock(key);var items=FashionItem9.GetArray(b.Data);foreach(ref var item in items.AsSpan())item.Clear();FashionItem9.SetArray(items,b.Data);}}
    if(save is SAV9ZA za){var item=new FashionItem9a{Value=1001,IsOwned=false};item.Write(za.Blocks.GetBlock(SaveBlockAccessor9ZA.KFashionTops).Data[..8]);}
    var session=NewSession(save);var fashion=Request(session,new{op="fashion"});var row=fashion.GetProperty("items").EnumerateArray().First();string id=row.GetProperty("id").GetString()!;uint target=Convert.ToUInt32(id.Split(':')[0],16);
    var prior=((ISCBlockArray)save).AllBlocks.ToDictionary(b=>b.Key,b=>b.Data.ToArray());
    Request(session,new{op="wardrobeSet",id,owned=true,revision=Revision(session)});
    var active=Current(session);foreach(var block in ((ISCBlockArray)active).AllBlocks)if(block.Key!=target&&!block.Data.SequenceEqual(prior[block.Key]))throw new Exception("Unrelated wardrobe block changed");
    var expected=Request(session,new{op="fashion"}).GetProperty("items").ToString();var output=active.Write();SaveFile reopened=version switch{GameVersion.PLA=>new SAV8LA(output),GameVersion.SL=>new SAV9SV(output),_=>new SAV9ZA(output)};
    saveField.SetValue(session,reopened);if(!reopened.ChecksumsValid||Request(session,new{op="fashion"}).GetProperty("items").ToString()!=expected)throw new Exception("Wardrobe serialization mismatch");
    Request(session,new{op="undo"});if(Request(session,new{op="fashion"}).GetProperty("items").ToString()!=fashion.GetProperty("items").ToString())throw new Exception("Wardrobe undo mismatch");
    Console.WriteLine($"PASS {version} wardrobe unrelated-block preservation, serialization/checksums and Undo");
}
foreach(var version in new[]{GameVersion.Pt,GameVersion.X,GameVersion.AS,GameVersion.US,GameVersion.BD}){
    var fixture=version==GameVersion.Pt?new SAV4Pt(new byte[0x80000]):BlankSaveFile.Get(version,"Fixture");var session=NewSession(fixture);var collections=Request(session,new{op="treats"}).GetProperty("collections").EnumerateArray().ToArray();
    foreach(var collection in collections){string kind=collection.GetProperty("value").GetString()!;Request(session,new{op="treatsSet",kind,mode="give",revision=Revision(session)});var expected=Request(session,new{op="treats",kind}).GetProperty("entries").ToString();var output=Current(session).Write();SaveFile reopened=version switch{GameVersion.Pt=>new SAV4Pt(output),GameVersion.X=>new SAV6XY(output),GameVersion.AS=>new SAV6AO(output),GameVersion.US=>new SAV7USUM(output),_=>new SAV8BS(output)};saveField.SetValue(session,reopened);if(!reopened.ChecksumsValid||Request(session,new{op="treats",kind}).GetProperty("entries").ToString()!=expected)throw new Exception("Treat serialization mismatch "+version);}
    Console.WriteLine($"PASS {version} all supported treat cases serialized, reparsed and checksum-valid");
}
Console.WriteLine($"PASS {checks} direct adapter operations; synthetic fixtures, no personal save modified");
