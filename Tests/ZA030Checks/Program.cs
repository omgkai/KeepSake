using System.Reflection;
using System.Text.Json;
using PKHeX.Core;
var type=Assembly.Load("PKHeXBridge").GetType("EditorSession")!;var sf=type.GetField("save",BindingFlags.Instance|BindingFlags.NonPublic)!;object session=Activator.CreateInstance(type,new object[]{null!})!;int checks=0;
var sav=(SAV9ZA)BlankSaveFile.Get(GameVersion.ZA,"ZA checks");sav.LastSaved.Timestamp=new DateTime(2026,9,26);sf.SetValue(session,sav);
JsonElement Req(object payload){checks++;using var d=JsonDocument.Parse(JsonSerializer.Serialize(payload));try{return JsonSerializer.SerializeToElement(type.GetMethod("Handle")!.Invoke(session,new object[]{d.RootElement})!);}catch(TargetInvocationException e){throw e.InnerException!;}}
void Assert(bool b,string why){checks++;if(!b)throw new Exception(why);}
string S(JsonElement r,string f)=>r.GetProperty(f).GetString()!;
SAV9ZA Save()=>(SAV9ZA)sf.GetValue(session)!;
string Finger()=>string.Join(";",Save().AllBlocks.Select(b=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b.Data))));
int Rev()=>Req(new{op="state"}).GetProperty("revision").GetInt32();
JsonElement Page(string group,string search="",int offset=0,bool showEmpty=false)=>Req(new{op="zaEvents",group,search,offset,showEmpty});
void Edit(string group,int index,params(string field,string value)[] edits)=>Req(new{op="zaEventSet",group,index,revision=Rev(),mode="edit",edits=edits.Select(e=>new{e.field,e.value})});
void Clear(string group,int index)=>Req(new{op="zaEventSet",group,index,revision=Rev(),mode="clear"});
void Reject(Action action){var before=Finger();int rev=Rev();bool failed=false;try{action();}catch{failed=true;}Assert(failed,"Expected rejection");Assert(Finger()==before&&Rev()==rev,"Rejected edit changed save");}
var groups=Page("Flags").GetProperty("groups").EnumerateArray().Select(g=>S(g,"value")).ToArray();Assert(groups.Length==15,"All 15 Windows collections");
foreach(string group in groups){
 var storage=Save().Blocks.GetType().GetProperty(group)!.GetValue(Save().Blocks)!;var st=storage.GetType();int count=(int)st.GetProperty("Count")!.GetValue(storage)!;for(int i=0;i<count;i++)st.GetMethod("Clear")!.Invoke(storage,new object[]{i});
 Assert(Page(group).GetProperty("total").GetInt32()==0,"Empty filtering");var page=Page(group,showEmpty:true);Assert(page.GetProperty("entries").GetArrayLength()==Math.Min(100,count),"Page size");if(count>100)Assert(Page(group,offset:100,showEmpty:true).GetProperty("entries")[0].GetProperty("id").GetString()=="100","Page offset");
 var row=page.GetProperty("entries")[0];var edits=row.GetProperty("fields").EnumerateArray().Select(f=>(S(f,"id"),S(f,"kind")=="bool"?"true":S(f,"kind")=="text"?"example_"+group+"_"+S(f,"id"):ulong.MaxValue.ToString())).ToArray();string before=Finger();Edit(group,0,edits);var after=Page(group);Assert(after.GetProperty("total").GetInt32()==1,"Added entry");Assert(after.GetProperty("entries")[0].GetProperty("fields").EnumerateArray().Last().GetProperty("value").GetString()==edits.Last().Item2,"Full-width value");Req(new{op="undo"});Assert(Finger()==before,"Exact undo");Req(new{op="redo"});Assert(Page(group).GetProperty("total").GetInt32()==1,"Redo");
 string key=after.GetProperty("entries")[0].GetProperty("fields")[0].GetProperty("value").GetString()!;Assert(Page(group,key).GetProperty("total").GetInt32()==1,"Key search");
 Edit(group,1,edits.Select(x=>(x.Item1,x.Item1=="key0"?"another_"+group:x.Item2)).ToArray());Clear(group,0);Assert(Page(group).GetProperty("total").GetInt32()==1&&Page(group).GetProperty("entries")[0].GetProperty("id").GetString()=="0","Removal compacts");Req(new{op="undo"});Assert(Page(group).GetProperty("total").GetInt32()==2,"Removal undo");
 Reject(()=>Edit(group,count,("value","1")));Reject(()=>Edit(group,0,("value","-1")));Reject(()=>Edit(group,0,("value","18446744073709551616")));Reject(()=>Edit(group,0,("unknown","1")));Reject(()=>Edit(group,0,("key0","x"),("key0","y")));Reject(()=>Req(new{op="zaEventSet",group,index=0,revision=-1,mode="clear"}));
 Console.WriteLine("PASS "+group+" keys, values, paging, compaction, bounds and undo");
}
var scratch=Path.Combine(Path.GetTempPath(),"keepsake-za030-"+Guid.NewGuid());Directory.CreateDirectory(scratch);string names=Path.Combine(scratch,"names.txt");File.WriteAllText(names,"0123456789ABCDEF\tCustom flag\n");Req(new{op="zaEventNames",path=names});Edit("Flags",0,("key0","Custom flag"),("value","true"));Assert(Page("Flags","Custom flag").GetProperty("total").GetInt32()==1,"Imported key label");Assert(Save().Blocks.Flags.GetKey(0)==0x0123456789ABCDEF,"Known name resolution");
// Give the synthetic blank blocks concrete serialization types, then fit one opaque block to a recognized file size.
var ctor=typeof(SCBlock).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(uint),typeof(SCTypeCode),typeof(Memory<byte>)},null)!;
var blocks=Save().AllBlocks.Select(b=>(SCBlock)ctor.Invoke(new object[]{b.Key,b.Type!=SCTypeCode.None?b.Type:b.Data.Length==0?SCTypeCode.Bool1:SCTypeCode.Object,new Memory<byte>(b.Data.ToArray())})).ToList();
int target=typeof(SaveUtil).GetFields(BindingFlags.NonPublic|BindingFlags.Static).Where(f=>f.Name.StartsWith("SIZE_G9ZA")&&f.FieldType==typeof(int)).Select(f=>(int)f.GetValue(null)!).Max();
int difference=target-SwishCrypto.Encrypt(blocks).Length;var meta=new SCBlockMetadata(Save().Blocks,[]);var opaque=blocks.Where(b=>b.Type==SCTypeCode.Object&&b.Data.Length+difference>=16&&meta.GetBlockName(b,out _)==null).OrderByDescending(b=>b.Data.Length).First();int position=blocks.IndexOf(opaque);var resized=new byte[opaque.Data.Length+difference];opaque.Data[..Math.Min(opaque.Data.Length,resized.Length)].CopyTo(resized);blocks[position]=(SCBlock)ctor.Invoke(new object[]{opaque.Key,opaque.Type,new Memory<byte>(resized)});
string path=Path.Combine(scratch,"before.sav");File.WriteAllBytes(path,SwishCrypto.Encrypt(blocks));Assert(SaveUtil.TryGetSaveFile(path,out var recognized),"Recognized fixture");sf.SetValue(session,recognized);
var comparison=Req(new{op="zaEventCompare",previous=path,updated=""});Assert(comparison.GetProperty("entries").GetArrayLength()==0,"Unchanged comparison");Edit("Flags",0,("value","false"));comparison=Req(new{op="zaEventCompare",previous=path,updated=""});Assert(comparison.GetProperty("entries").GetArrayLength()==1,"Changed comparison");
string report=Path.Combine(scratch,"report.txt");Req(new{op="eventCompareExport",token=S(comparison,"token"),path=report});Assert(File.ReadAllText(report)==S(comparison,"report"),"Exact report export");Reject(()=>Req(new{op="eventCompareExport",token=S(comparison,"token"),path}));
Clear("Flags",0);comparison=Req(new{op="zaEventCompare",previous=path,updated=""});Assert(comparison.GetProperty("entries").EnumerateArray().Any(e=>S(e,"after")=="Removed"),"Removed key detection");
var bytes=Save().Write().ToArray();Assert(SwishCrypto.GetIsHashValid(bytes)&&new SAV9ZA(bytes).ChecksumsValid,"Serialized hash and checksums");
Console.WriteLine($"PASS {checks} Z-A event requests/assertions");
