using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;int checks=0;
object Session(SaveFile save){var s=Activator.CreateInstance(type,new object[]{null!})!;saveField.SetValue(s,save);return s;}
SaveFile Save(object s)=>(SaveFile)saveField.GetValue(s)!;
JsonElement Req(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw();throw;}}
int Rev(object s)=>Req(s,new{op="state"}).GetProperty("revision").GetInt32();
void Edit(object s,string kind,string id,params (string field,string value)[] values)=>Req(s,new{op="extraSet",kind,id,revision=Rev(s),edits=values.Select(v=>new{v.field,v.value})});
void Act(object s,string kind,string id,string mode)=>Req(s,new{op="extraSet",kind,id,mode,revision=Rev(s)});
JsonElement Row(object s,string kind,string id)=>Req(s,new{op="extraEntry",kind,id});
string Value(object s,string kind,string id,string key)=>Row(s,kind,id).GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==key).GetProperty("value").GetString()!;
void Assert(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Reject(Action call,string why){try{call();}catch{checks++;return;}throw new Exception("Expected rejection: "+why);}
byte[] GBA(){var data=new byte[0x20000];for(int i=0;i<28;i++){System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i*0x1000+0xFF4),(short)(i%14));System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i*0x1000+0xFF8),0x08012025);if(i%14==0)data.AsSpan(i*0x1000,8).Fill(255);}return data;}
SaveFile Fixture(GameVersion game)=>game switch {GameVersion.Stadium2=>new SAV2Stadium(false),GameVersion.R or GameVersion.S=>new SAV3RS(GBA()){Version=game,Language=2},GameVersion.E=>new SAV3E(GBA()){Language=2},GameVersion.FR or GameVersion.LG=>new SAV3FRLG(GBA()){Version=game,Language=2},GameVersion.D or GameVersion.P=>new SAV4DP(new byte[0x80000]){Version=game},GameVersion.Pt=>new SAV4Pt(new byte[0x80000]),GameVersion.HG or GameVersion.SS=>new SAV4HGSS(new byte[0x80000]){Version=game},_=>BlankSaveFile.Get(game,"Parity")};
SaveFile ReadSave(SaveFile source,byte[] data)=>source switch {SAV2=>new SAV2(data,(LanguageID)source.Language,source.Version),SAV2Stadium=>new SAV2Stadium(data,source.Language==1),SAV3RS=>new SAV3RS(data){Version=source.Version,Language=source.Language},SAV3E=>new SAV3E(data),SAV3FRLG=>new SAV3FRLG(data){Version=source.Version,Language=source.Language},SAV4DP=>new SAV4DP(data),SAV4Pt=>new SAV4Pt(data),SAV4HGSS=>new SAV4HGSS(data),SAV5BW=>new SAV5BW(data),SAV5B2W2=>new SAV5B2W2(data),_=>SaveUtil.GetSaveFile(data)!};
foreach(var game in new[]{GameVersion.GD,GameVersion.C,GameVersion.Stadium2,GameVersion.R,GameVersion.E,GameVersion.FR,GameVersion.D,GameVersion.Pt,GameVersion.HG,GameVersion.B,GameVersion.W2}){
 var session=Session(Fixture(game));var sav=Save(session);int item=sav.Generation switch{2=>158,3=>121,_=>137};int party=sav.Generation<4?6:1;int max=sav.Generation<4?16:21;
 if(sav is SAV2Stadium){party=30;max=80;}else{var pk=sav.BlankPKM;pk.Species=25;pk.CurrentLevel=10;pk.Nickname="Partner";pk.HeldItem=item;if(pk is PK3 p3)p3.HeldMailID=0;sav.SetPartySlotAtIndex(pk,0,EntityImportSettings.None);}
 var before=sav.Write().ToArray();var page=Req(session,new{op="extraPage",kind="mail"});Assert(before.SequenceEqual(Save(session).Write().ToArray()),game+" mail read purity");
 if(sav.Generation==2)Edit(session,"mail","capacity",("Count",sav is SAV2Stadium?"50":"10"));
 foreach(int i in new[]{0,party,max-1}){
  var changes=new List<(string field,string value)>{("MailType",sav.Generation<=3?item.ToString():"0"),("AuthorName","KAIKAI"),("AuthorTID","12345")};
  if(sav.Generation==2)changes.AddRange([("Species","25"),("Line1","HELLO FRIEND"),("Line2","KEEP IN TOUCH"),("UserEntered","true")]);
  else {changes.Add(("AuthorSID","54321"));for(int y=0;y<3;y++)for(int x=0;x<(sav.Generation==3?3:4);x++)changes.Add(($"Word{y}{x}",(50+y*4+x).ToString()));if(sav.Generation==3)changes.Add(("Species","25"));else changes.Add(("Gender","1"));if(sav.Generation==4)changes.AddRange([("Portrait0","25"),("Portrait1","133"),("Portrait2","0")]);if(sav.Generation==5)changes.AddRange([("Misc0","123"),("Misc1","456"),("Misc2","789"),("MessageEnding","65535")]);}
  Edit(session,"mail","letter:"+i,changes.ToArray());foreach(var c in changes)Assert(Value(session,"mail","letter:"+i,c.field).TrimEnd()==c.value,game+" letter "+i+" "+c.field+" got "+Value(session,"mail","letter:"+i,c.field));
  // Changing the trainer ID must preserve encoded names/message bytes.
  Edit(session,"mail","letter:"+i,("AuthorTID","23456"));Assert(Value(session,"mail","letter:"+i,"AuthorName")=="KAIKAI","Unrelated edit preserves author");if(sav.Generation==2)Assert(Value(session,"mail","letter:"+i,"Line1").TrimEnd()=="HELLO FRIEND","Encoded Gen2 message preserved");
 }
 var expected=Req(session,new{op="extraEntry",kind="mail",id="letter:"+(max-1)}).ToString();var bytes=Save(session).Write().ToArray();var copy=ReadSave(Save(session),bytes);Assert(copy!=null&&copy.ChecksumsValid,game+" mail save checksum");saveField.SetValue(session,copy);Assert(Req(session,new{op="extraEntry",kind="mail",id="letter:"+(max-1)}).ToString()==expected,game+" mail serialization");
 before=Save(session).Write().ToArray();Reject(()=>Edit(session,"mail","letter:0",("AuthorTID","65536")),"Mail numeric bound");Reject(()=>Edit(session,"mail","letter:"+max,("AuthorTID","1")),"Mail slot bounds");Assert(before.SequenceEqual(Save(session).Write().ToArray()),"Rejected mail atomicity");
 Act(session,"mail","letter:"+(max-1),"clearMail");Assert(Value(session,"mail","letter:"+(max-1),"MailType")== (sav.Generation<4?"0":"255"),"Clear truly empty");Req(session,new{op="undo"});Assert(Req(session,new{op="extraEntry",kind="mail",id="letter:"+(max-1)}).ToString()==expected,"Mail clear Undo");
 if(sav is not SAV2Stadium){Act(session,"mail","letter:0","detachMail");Assert(Save(session).GetPartySlotAtIndex(0).HeldItem==0,"Detach held item");if(Save(session).GetPartySlotAtIndex(0) is PK3 three)Assert(three.HeldMailID==-1,"Detach Gen3 reference");Req(session,new{op="undo"});if(sav.Generation<=3){Edit(session,"mail","letter:"+(party+1),("AuthorTID","101"));Act(session,"mail","letter:"+party,"downMail");Assert(Value(session,"mail","letter:"+(party+1),"AuthorTID")=="23456"&&Value(session,"mail","letter:"+party,"AuthorTID")=="101","Mail reorder bytes");}}
 Console.WriteLine("PASS "+game+" mailbox author/message/portraits, serialized readback, read purity, validation, clear/detach and Undo");
}

foreach(var language in new[]{LanguageID.Japanese,LanguageID.Korean,LanguageID.German}){
 var sav=new SAV2(language,GameVersion.GD);var session=Session(sav);string name=language==LanguageID.Japanese?"カイ":language==LanguageID.Korean?"가나":"KAI";
 Edit(session,"mail","letter:6",("MailType","158"),("AuthorName",name),("Line1",name),("Line2",name),("AuthorTID","456"),("UserEntered","true"));
 Assert(Value(session,"mail","letter:6","AuthorName")==name,"Localized author "+language);Assert(Value(session,"mail","letter:6","Line1").TrimEnd()==name,"Localized message "+language);Edit(session,"mail","letter:6",("AuthorTID","789"));Assert(Value(session,"mail","letter:6","Line1").TrimEnd()==name,"Localized unrelated edit "+language);
 var data=Save(session).Write();saveField.SetValue(session,new SAV2(data,language,GameVersion.GD));Assert(Save(session).ChecksumsValid&&Value(session,"mail","letter:6","AuthorName")==name,"Localized saved mail "+language);Console.WriteLine("PASS "+language+" Gen2 mail encoding and serialized readback");
}
Console.WriteLine($"PASS {checks} mail checks");
