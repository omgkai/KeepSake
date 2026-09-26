using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
using System.Buffers.Binary;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;int checks=0;
JsonElement Call(object session,object payload){checks++;using var d=JsonDocument.Parse(JsonSerializer.Serialize(payload));try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(session,new object[]{d.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
var saveField=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;
foreach(string version in new[]{"RD","GD","E","HG","B2","X","AS","US","SW","BD","PLA","SL","ZA"}) {
 var session=Activator.CreateInstance(type,new object[]{null!})!;Call(session,new{op="demo",version});
 SaveFile Save()=>(SaveFile)saveField.GetValue(session)!;
 if(Save() is SAV3E) {var data=new byte[0x20000];for(int i=0;i<28;i++){BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i*0x1000+0xFF4),(short)(i%14));BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(i*0x1000+0xFF8),0x08012025);}var full=new SAV3E(data);full.CopyChangesFrom(Save());saveField.SetValue(session,full);}
 if(Save() is SAV4HGSS) {var full=new SAV4HGSS(new byte[0x80000]);full.CopyChangesFrom(Save());saveField.SetValue(session,full);}
 PKM Pokemon(ushort species){var pk=Save().BlankPKM;pk.Species=species;pk.CurrentLevel=25;pk.RefreshChecksum();return pk;}
 if(!Save().HasParty)continue;
 Save().SetPartySlotAtIndex(Pokemon(25),0,EntityImportSettings.None);
 Save().SetPartySlotAtIndex(Pokemon(133),1,EntityImportSettings.None);
 Save().SetPartySlotAtIndex(Pokemon(1),2,EntityImportSettings.None);
 Save().SetBoxSlotAtIndex(Pokemon(4),1,0,EntityImportSettings.None);
 var original=Save().Write().ToArray();
 void Swap(int fs,bool fp,int ts,bool tp,bool valid=true){var state=Call(session,new{op="state"});var bytes=Save().Write().ToArray();try{Call(session,new{op="slotSwap",session=state.GetProperty("dragSession").GetString(),revision=state.GetProperty("revision").GetInt32(),fromBox=1,fromSlot=fs,fromParty=fp,toBox=1,toSlot=ts,toParty=tp});if(!valid)throw new Exception("Expected rejection");}catch(Exception ex)when(!valid){var after=Save().Write().ToArray();Check(bytes.SequenceEqual(after),$"{version}: failed {fs}/{fp}->{ts}/{tp}, party {Save().PartyCount}, {ex.Message}, byte differences {string.Join(",",bytes.Zip(after).Select((x,i)=>(x,i)).Where(x=>x.x.First!=x.x.Second).Take(12).Select(x=>x.i))}");}}
 void Undo(){Call(session,new{op="undo"});Check(original.SequenceEqual(Save().Write().ToArray()),version+" exact undo");}
 Swap(0,true,1,true);Check(Save().GetPartySlotAtIndex(0).Species==133 && Save().GetPartySlotAtIndex(1).Species==25,"party swap");Undo();
 Swap(0,true,3,true);Check(Save().PartyCount==3 && Save().GetPartySlotAtIndex(2).Species==25 && Save().GetPartySlotAtIndex(0).Species==133,"party append");Undo();
 Swap(1,true,1,false);Check(Save().PartyCount==2 && Save().GetPartySlotAtIndex(1).Species==1 && Save().GetBoxSlotAtIndex(1,1).Species==133,"party to empty box compaction");Undo();
 Swap(0,false,3,true);Check(Save().PartyCount==4 && Save().GetPartySlotAtIndex(3).Species==4 && Save().GetBoxSlotAtIndex(1,0).Species==0,"box to appended party");Undo();
 Swap(0,false,1,true);Check(Save().PartyCount==3 && Save().GetPartySlotAtIndex(1).Species==4 && Save().GetBoxSlotAtIndex(1,0).Species==133,"box-party swap");Undo();
 Swap(0,false,5,true,false);Swap(4,true,0,false,false);
 Save().DeletePartySlot(2);Save().DeletePartySlot(1);Swap(0,true,1,false,false);
 Console.WriteLine("PASS party transfers, boundaries and byte-exact Undo: "+version);
}
Console.WriteLine($"PASS {checks} checks");
