using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;
var field=type.GetField("entity",BindingFlags.Instance|BindingFlags.NonPublic)!;
int checks=0;
object Session(PKM pk){var s=Activator.CreateInstance(type,new object[]{null!})!;field.SetValue(s,pk.Clone());return s;}
PKM Current(object s)=>(PKM)field.GetValue(s)!;
JsonElement Req(object s,object r){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(r));checks++;try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(s,new object[]{doc.RootElement})!);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw();throw;}}
void Assert(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
string[] Choices(object s)=>Req(s,new{op="lookup",kind="entityAbilities"}).EnumerateArray().Select(c=>c.GetProperty("value").GetString()!).ToArray();
void Edit(object s,string id,string value)=>Req(s,new{op="entityEdit",edits=new[]{new{field=id,value}}});
foreach(PKM pk in new PKM[]{new PK1(),new PK2(),new PK3(),new CK3(),new XK3(),new PK4(),new BK4(),new PK5(),new PK6(),new PK7(),new PB7(),new PK8(),new PB8(),new PA8(),new PK9(),new PA9()}) {
 pk.Species=25;if(pk.Format is >=3 and <=5)pk.Version=pk.Format==3?GameVersion.E:pk.Format==4?GameVersion.Pt:GameVersion.W;var s=Session(pk);var before=Current(s).Data.ToArray();var choices=Choices(s);
 Assert(before.SequenceEqual(Current(s).Data.ToArray()),"Lookup read purity "+pk.GetType().Name);
 if(pk.Format<3){Assert(choices.Length==0,"No GB abilities");continue;}
 var expected=Enumerable.Range(0,pk.PersonalInfo.AbilityCount).Where(i=>!(pk.Format==3&&i==1&&pk.PersonalInfo.GetAbilityAtIndex(1)==pk.PersonalInfo.GetAbilityAtIndex(0))).Where(i=>pk.PersonalInfo.GetAbilityAtIndex(i)>0&&pk.PersonalInfo.GetAbilityAtIndex(i)<=pk.MaxAbilityID).Select(i=>$"{pk.PersonalInfo.GetAbilityAtIndex(i)}:{i}").ToArray();
 Assert(choices.SequenceEqual(expected),"Exact native slots "+pk.GetType().Name);
 foreach(var choice in choices){Edit(s,"AbilityChoice",choice);var parts=choice.Split(':');Assert(Current(s).Ability==int.Parse(parts[0]),"Chosen ability "+pk.GetType().Name);Assert(Current(s).AbilityNumber==(1<<int.Parse(parts[1])),"Chosen slot "+pk.GetType().Name);}
 before=Current(s).Data.ToArray();bool rejected=false;try{Edit(s,"AbilityChoice","999:2");}catch{rejected=true;}Assert(rejected&&before.SequenceEqual(Current(s).Data.ToArray()),"Invalid choice atomicity");
 Console.WriteLine("PASS "+pk.GetType().Name+" contextual slots, selection and read purity");
}
var formSession=Session(new PK9{Species=110});Assert(Choices(formSession).Contains("26:0"),"Kantonian Weezing Levitate");Edit(formSession,"Form","1");Assert(Choices(formSession).Contains("228:2"),"Galarian Weezing Misty Surge after form change");Edit(formSession,"Species","25");Edit(formSession,"Form","0");Assert(Choices(formSession).Contains("9:0")&&!Choices(formSession).Contains("228:2"),"Species refresh");
var dup=Session(new PK9{Species=1});Edit(dup,"AbilityChoice","65:1");Assert(Current(dup).AbilityNumber==2,"Duplicate normal slot preserved");Edit(dup,"AbilityChoice","34:2");Assert(Current(dup).AbilityNumber==4&&Current(dup).Ability==34,"Hidden slot");
var blue=Session(new PK5{Species=550,Form=1,Version=GameVersion.W});Assert(Choices(blue).Contains("69:0"),"Gen 5 traded Basculin choice");Edit(blue,"AbilityChoice","69:0");Assert(Current(blue).Ability==69&&Current(blue).AbilityNumber==1,"Gen 5 traded choice assignment");
Assert(!Req(Session(new PK6{Species=550,Form=1}),new{op="lookup",kind="entityAbilities"}).ToString().Contains("Traded ability"),"Basculin extra label only Gen 5");
var invalid=Session(new PK9{Species=25,Ability=1});Assert(!Choices(invalid).Any(c=>c.StartsWith("1:"))&&Current(invalid).Ability==1,"Illegal stored value preserved but not offered");
Assert(Choices(Session(new PK9())).Length==0,"Empty entity");
var ralts=Session(new PK3{Species=280,Version=GameVersion.E});foreach(var choice in Choices(ralts)){Edit(ralts,"AbilityChoice",choice);Assert(Current(ralts).AbilityNumber==1<<int.Parse(choice.Split(':')[1]),"Gen 3 two distinct abilities");}
var stored=Session(new PK9{Species=25,Ability=9,AbilityNumber=1,Version=GameVersion.VL});Edit(stored,"AbilityChoice","31:2");Assert(Current(stored).Ability==31&&Current(stored).AbilityNumber==4,"Hidden Lightning Rod");Req(stored,new{op="undo"});Assert(Current(stored).Ability==9&&Current(stored).AbilityNumber==1,"Ability Undo");Req(stored,new{op="redo"});Assert(Current(stored).Ability==31&&Current(stored).AbilityNumber==4,"Ability Redo");
var file=Path.Combine(Path.GetTempPath(),"keepsake-ability-"+Guid.NewGuid()+".pk9");try{Req(stored,new{op="exportEntity",path=file});Req(stored,new{op="open",path=file});Assert(Current(stored).Ability==31&&Current(stored).AbilityNumber==4&&Current(stored).ChecksumValid,"Export/reopen hidden slot");}finally{File.Delete(file);}
Console.WriteLine($"PASS {checks} requests/assertions");
