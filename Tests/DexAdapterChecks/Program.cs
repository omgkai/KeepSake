using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
int checks=0;
foreach(bool legacy in new[]{false,true}) {
    var blank=(SAV9SV)BlankSaveFile.Get(GameVersion.SL,"Fixture");
    var constructor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
    var blocks=blank.AllBlocks.Where(b=>!legacy || b.Key!=0xF5D7C0E2)
        .Select(b=>(SCBlock)constructor.Invoke(new object[]{b.Key,b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object,new Memory<byte>(b.Data.ToArray())})).ToArray();
    var save=new SAV9SV(SwishCrypto.Encrypt(blocks));
    var session=Activator.CreateInstance(type,new object[]{null!})!;
    var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;saveField.SetValue(session,save);
    object Request(object request) {using var json=JsonDocument.Parse(JsonSerializer.Serialize(request));checks++;return type.GetMethod("Handle")!.Invoke(session,new object[]{json.RootElement})!;}
    JsonElement Read() => JsonSerializer.SerializeToElement(Request(new{op="dexRecord",species=25}));
    var flags=JsonSerializer.SerializeToElement(Request(new{op="dex"}));
    if(!flags.GetProperty("supported").GetBoolean())throw new Exception("SV dex incorrectly hidden");
    Request(new{op="dexSet",species=25,seen=true,caught=true});
    var activeSave=(SAV9SV)saveField.GetValue(session)!;
    if(!activeSave.GetSeen(25)||!activeSave.GetCaught(25))throw new Exception("SV flags not set");
    var flagCopy=new SAV9SV(activeSave.Write());
    if(!flagCopy.GetCaught(25)||!flagCopy.ChecksumsValid)throw new Exception("SV flags serialization mismatch");
    Request(new{op="undo"});
    if(JsonSerializer.SerializeToElement(Request(new{op="dex"})).GetProperty("entries").ToString()!=flags.GetProperty("entries").ToString())throw new Exception("SV flag undo mismatch");
    save=(SAV9SV)saveField.GetValue(session)!;
    var initial=Read();var prior=save.AllBlocks.ToDictionary(b=>b.Key,b=>b.Data.ToArray());
    foreach(var field in initial.GetProperty("fields").EnumerateArray()) {
        string id=field.GetProperty("id").GetString()!;var old=Read();
        string value=field.GetProperty("kind").GetString()=="bool" ? "true" : field.GetProperty("choices").EnumerateArray().Last().GetProperty("value").GetString()!;
        Request(new{op="dexRecordSet",species=25,field=id,value});
        var now=Read();if(now.GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==id).GetProperty("value").GetString()!=value)throw new Exception(id);
        Request(new{op="undo"});if(Read().ToString()!=old.ToString())throw new Exception("Undo "+id);
        Request(new{op="redo"});
    }
    var edited=(SAV9SV)saveField.GetValue(session)!;
    uint active=legacy?0x0DEAAEBD:0xF5D7C0E2;
    foreach(var block in edited.AllBlocks)if(block.Key!=active && !block.Data.SequenceEqual(prior[block.Key]))throw new Exception("Unrelated block changed: "+block.Key);
    var expected=Read().ToString();var bytes=edited.Write();var reopened=new SAV9SV(bytes);saveField.SetValue(session,reopened);
    if(Read().ToString()!=expected || !reopened.ChecksumsValid)throw new Exception("Serialization mismatch");
    Console.WriteLine($"PASS {(legacy?"original Paldea":"Kitakami/Blueberry")} adapter field writes, Undo/Redo, inactive-block preservation, serialized readback. Synthetic buffers are not playable game saves.");
}
Console.WriteLine($"PASS {checks} adapter checks");
