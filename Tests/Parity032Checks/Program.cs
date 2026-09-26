using System.Reflection;
using System.Text.Json;
using System.Buffers.Binary;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;int checks=0;
JsonElement Call(object session,object payload){checks++;using var d=JsonDocument.Parse(JsonSerializer.Serialize(payload));try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(session,new object[]{d.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
string S(JsonElement e,string k)=>e.GetProperty(k).GetString()!;
void Check(bool b){checks++;if(!b)throw new Exception("Assertion failed");}
SAV3E Emerald(bool japanese){var data=new byte[0x20000];for(int i=0;i<28;i++){BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i*0x1000+0xFF4),(short)(i%14));BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i*0x1000+0xFF8),0x08012025);if(i%14==0)data.AsSpan(i*0x1000,8).Fill(255);}return new SAV3E(data){Language=japanese?1:2};}
foreach(bool japanese in new[]{false,true}) {
 foreach(SaveFile save in new SaveFile[]{new SAV1(japanese?LanguageID.Japanese:LanguageID.English),Emerald(japanese),new SAV6XY()}) {
  var session=Activator.CreateInstance(type,new object[]{null!})!;var sf=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;sf.SetValue(session,save);
  var original=save.Write();
  foreach(var field in save is SAV6 ? new[]{"Nickname","OriginalTrainerName"}:new[]{"Nickname"}) {
   var info=Call(session,new{op="fameNameBytesInfo",id="0:0",field});string text=japanese?"アイ":"AB";
   var preview=Call(session,new{op="fameNameBytesInfo",id="0:0",field,revision=info.GetProperty("revision").GetInt32(),entityKey=S(info,"entityKey"),hex=S(info,"hex"),mode="text",text});Check(S(preview,"text")==text);
   Call(session,new{op="fameNameBytesSet",id="0:0",field,revision=preview.GetProperty("revision").GetInt32(),entityKey=S(preview,"entityKey"),hex=S(preview,"hex")});
   var modified=(SaveFile)sf.GetValue(session)!;var bytes=modified.Write();SaveFile reparsed=modified switch{SAV1=>new SAV1(bytes,japanese?LanguageID.Japanese:LanguageID.English,GameVersion.RD),SAV3E=>new SAV3E(bytes){Language=japanese?1:2},_=>new SAV6XY(bytes)};
   sf.SetValue(session,reparsed);Check(S(Call(session,new{op="fameNameBytesInfo",id="0:0",field}),"text")==text);
   Call(session,new{op="undo"});Check(((SaveFile)sf.GetValue(session)!).Write().Span.SequenceEqual(original.Span));
  }
 }
}
Console.WriteLine($"PASS {checks} Hall of Fame checks: Japanese/English, Gen 1/3/6 serialization and exact Undo");
