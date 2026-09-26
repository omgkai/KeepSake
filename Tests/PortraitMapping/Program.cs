using System.Text.Json;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
var strings=GameInfo.GetStrings("en");var result=new List<object>();
foreach(var version in new[]{GameVersion.E,GameVersion.Pt,GameVersion.B2,GameVersion.AS,GameVersion.US,GameVersion.GP,GameVersion.SW,GameVersion.BD,GameVersion.PLA,GameVersion.VL,GameVersion.ZA}) {
 var save=BlankSaveFile.Get(version,"Artwork");var table=save.Personal;
 for(ushort sp=1;sp<=save.MaxSpeciesID;sp++) {
  var names=FormConverter.GetFormList(sp,strings.Types,strings.forms,GameInfo.GenderSymbolASCII,save.Context);
  var count=Math.Max(table[sp].FormCount,names.Length);
  for(byte form=0;form<count;form++)for(byte gender=0;gender<2;gender++)for(uint arg=0;arg<(sp==869?7:1);arg++) {
   string sprite=sp==982&&form==1?"b_982-1":"b_"+SpriteName.GetResourceStringSprite(sp,form,gender,arg,save.Context)[1..].Replace('_','-');
   result.Add(new{species=sp,form,gender,argument=arg,context=save.Context.ToString(),name=strings.specieslist[sp],formName=form<names.Length?names[form]:"",showdown=ShowdownParsing.GetStringFromForm(form,strings,sp,save.Context),sprite});
  }
 }
}
Console.WriteLine(JsonSerializer.Serialize(result));
