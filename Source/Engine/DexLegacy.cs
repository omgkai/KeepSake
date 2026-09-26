using PKHeX.Core;
using System.Text.Json;

sealed partial class EditorSession {
    static bool LegacyDex(SaveFile? s)=>s is SAV4 or SAV5 or SAV6XY or SAV6AO or SAV7 or SAV7b or SAV8SWSH or SAV9ZA;
    List<(Field field,Action<string> write)> LegacyDexFields(int id) {
        var sav=RequireSave();if(id<1||id>sav.MaxSpeciesID)throw new Exception("Choose a species supported by this save.");ushort species=(ushort)id;
        var result=new List<(Field,Action<string>)>();
        void Flag(string key,string label,string group,bool value,Action<bool> write)=>result.Add((new(key,label,group,value?"true":"false","bool",true,"",null,[]),v=>{if(v is not ("true" or "false"))throw new Exception("Choose on or off.");write(v=="true");}));
        void Pick(string key,string label,string group,int value,Choice[] choices,Action<int> write)=>result.Add((new(key,label,group,value.ToString(),"enum",true,"",null,choices),v=>{if(!choices.Any(c=>c.value==v))throw new Exception("Choose a listed value.");write(int.Parse(v));}));
        void Number(string key,string label,string group,uint value,uint max,Action<uint> write)=>result.Add((new(key,label,group,value.ToString(),"number",true,$"0–{max}",null,[]),v=>{if(!uint.TryParse(v,out var n)||n>max)throw new Exception($"{label} must be between 0 and {max}.");write(n);}));
        var formNames=FormConverter.GetFormList(species,strings.Types,strings.forms,sav.Context);
        string FormName(int form)=>form<formNames.Length&&!string.IsNullOrWhiteSpace(formNames[form])?formNames[form]:form==0?"Normal":$"Form {form}";
        string[] appearances=["Male","Female","Shiny male","Shiny female"];
        if(sav is SAV4 s4) {
            var modes=(s4 is SAV4HGSS?new[]{"Not received","Regional Pokédex","National Pokédex","Other languages"}:new[]{"Not received","Regional Pokédex","Detect forms","National Pokédex","Other languages"}).Select((n,i)=>new Choice(i.ToString(),n)).ToArray();Pick("mode","Pokédex upgrade","Overview",ReadDexUpgrade(s4),modes,v=>SetDexUpgrade(s4,v));
            var z=s4.Dex;Flag("caught","Caught","Overview",z.GetCaught(species),v=>z.SetCaught(species,v));Flag("seen","Seen","Overview",z.GetSeen(species),v=>z.SetSeen(species,v));
            Pick("gender.first","First gender seen","Appearances",z.GetSeenGenderFirst(species),[new("0","Male / genderless"),new("1","Female")],v=>z.SetSeenGenderFirst(species,v));Pick("gender.second","Second gender seen","Appearances",z.GetSeenGenderSecond(species),[new("0","Male / genderless"),new("1","Female")],v=>z.SetSeenGenderSecond(species,v));
            if(z.HasLanguage(species))foreach(var lang in DexLanguages.Where(l=>l.Id<=7)){int bit=Zukan4.GetGen4LanguageBitIndex(lang.Id);Flag("language."+lang.Id,lang.Name,"Languages",z.GetLanguageBitIndex(species,bit),v=>z.SetLanguageBitIndex(species,bit,v));}
            var forms=z.GetForms(species);var choices=Enumerable.Range(0,forms.Length).Select(n=>new Choice(n.ToString(),FormName(n))).Append(new("255","Not recorded")).ToArray();
            for(int i=0;i<forms.Length;i++){int slot=i;Pick("formslot."+i,$"Seen order {i+1}","Forms",forms[i],choices,v=>{var current=z.GetForms(species);current[slot]=(byte)v;z.SetForms(species,current);});}
            if(species==327)Number("spinda","Spinda pattern PID","Appearance",z.SpindaPID,uint.MaxValue,v=>z.SpindaPID=v);
        } else if(sav is SAV5 || sav is SAV6XY or SAV6AO) {
            // Both generations share these operations but expose independent core classes.
            Zukan5? z5=(sav as SAV5)?.Zukan;Zukan6? z6=sav is SAV6XY xy?xy.Zukan:sav is SAV6AO ao?ao.Zukan:null;
            Flag("caught","Caught","Overview",z5?.GetCaught(species)??z6!.GetCaught(species),v=>{if(z5!=null)z5.SetCaught(species,v);else z6!.SetCaught(species,v);});
            Flag("national","National Pokédex unlocked","Overview",z5?.IsNationalDexUnlocked??z6!.IsNationalDexUnlocked,v=>{if(z5!=null)z5.IsNationalDexUnlocked=v;else z6!.IsNationalDexUnlocked=v;});
            Flag("nationalmode","National Pokédex mode","Overview",z5?.IsNationalDexMode??z6!.IsNationalDexMode,v=>{if(z5!=null)z5.IsNationalDexMode=v;else z6!.IsNationalDexMode=v;});
            if(z6 is Zukan6XY zx)Flag("foreign","Caught in another region","Overview",zx.GetForeignFlag(species),v=>zx.SetForeignFlag(species,v));
            if(z6 is Zukan6AO oras){Number("count.seen","Times encountered","DexNav",oras.GetCountSeen(species),ushort.MaxValue,v=>oras.SetCountSeen(species,(ushort)v));Number("count.obtained","Times obtained (unused by game)","DexNav",oras.GetCountObtained(species),ushort.MaxValue,v=>oras.SetCountObtained(species,(ushort)v));}
            if(z5!=null&&species==327)Number("spinda","Spinda pattern PID","Overview",z5.Spinda,uint.MaxValue,v=>z5.Spinda=v);
            if(z6!=null&&species==327)Number("spinda","Spinda pattern PID","Overview",z6.Spinda,uint.MaxValue,v=>z6.Spinda=v);
            for(int i=0;i<4;i++){int region=i;Flag("seen."+i,appearances[i],"Seen appearances",z5?.GetSeen(species,i)??z6!.GetSeen(species,i),v=>{if(z5!=null)z5.SetSeen(species,region,v);else z6!.SetSeen(species,region,v);});Flag("display."+i,appearances[i],"Displayed appearances",z5?.GetDisplayed(species,i)??z6!.GetDisplayed(species,i),v=>{if(z5!=null)z5.SetDisplayed(species,region,v);else z6!.SetDisplayed(species,region,v);});}
            foreach(var (lang,language) in DexLanguages.Take(7).Select((l,i)=>(l,i)).Where(_=>z5==null||species<=493)){Flag("language."+lang.Id,lang.Name,"Languages",z5?.GetLanguageFlag(species,language)??z6!.GetLanguageFlag(species,language),v=>{if(z5!=null)z5.SetLanguageFlag(species,language,v);else z6!.SetLanguageFlag(species,language,v);});}
            var fc=z5!=null?(z5.GetFormIndex(species).Index,z5.GetFormIndex(species).Count):((int)z6!.GetFormIndex(species).Index,z6.GetFormIndex(species).Count);
            for(int f=0;f<fc.Item2;f++)for(int region=0;region<4;region++){int index=fc.Item1+f,r=region;Flag($"form.{f}.{r}",(r>=2?"Display · ":"Seen · ")+(r%2==0?"Regular":"Shiny"),"Form: "+FormName(f),z5?.GetFormFlag(index,r)??z6!.GetFormFlag(index,r),v=>{if(z5!=null)z5.SetFormFlag(index,r,v);else z6!.SetFormFlag(index,r,v);});}
        } else if(sav is SAV7 or SAV7b) {
            var z=sav is SAV7 s7?s7.Zukan:((SAV7b)sav).Zukan;
            Flag("caught","Caught","Overview",z.GetCaught(species),v=>z.SetCaught(species,v));
            var entryNames=z.GetEntryNames(strings.specieslist);
            foreach(int index in new[]{id-1}.Concat(z.GetAllFormEntries(species)).Distinct())for(int region=0;region<4;region++){int bit=index,r=region;string group=index==id-1?"Normal appearance":index<entryNames.Count?entryNames[index]:$"Form entry {index}";Flag($"seen.{bit}.{r}","Seen · "+appearances[r],group,z.GetSeen((ushort)(bit+1),r),v=>z.SetSeen((ushort)(bit+1),r,v));Flag($"display.{bit}.{r}","Display · "+appearances[r],group,z.GetDisplayed(bit,r),v=>z.SetDisplayed(bit,r,v));}
            foreach(var (lang,index) in DexLanguages.Select((l,i)=>(l,i))){int bit=index;Flag("language."+bit,lang.Name,"Languages",z.GetLanguageFlag(id-1,bit),v=>z.SetLanguageFlag(id-1,bit,v));}
            if(z is Zukan7b lg)for(byte f=0;f<sav.Personal[species].FormCount;f++)if(Zukan7b.TryGetSizeEntryIndex(species,f,out _))foreach(var category in Enum.GetValues<DexSizeType>()){
                if((int)category>3)continue;byte form=f;var g=category;lg.GetSizeData(g,species,form,out byte height,out byte weight,out bool flag);string key=$"size.{form}.{(int)g}",group=$"{FormName(form)} · {Label(g.ToString())}";
                Number(key+".height","Height scalar",group,height,255,v=>{lg.GetSizeData(g,species,form,out _,out var w,out var b);lg.SetSizeData(g,species,form,(byte)v,w,b);});Number(key+".weight","Weight scalar",group,weight,255,v=>{lg.GetSizeData(g,species,form,out var h,out _,out var b);lg.SetSizeData(g,species,form,h,(byte)v,b);});Flag(key+".flag","Record flag",group,flag,v=>{lg.GetSizeData(g,species,form,out var h,out var w,out _);lg.SetSizeData(g,species,form,h,w,v);});
            }
        } else if(sav is SAV8SWSH sw) {
            var z=sw.Zukan;if(!z.GetEntry(species,out _))throw new Exception("This species is not listed in this save’s Pokédex.");
            Flag("caught","Caught","Overview",z.GetCaught(species),v=>z.SetCaught(species,v));Flag("gmax","Gigantamax obtained","Overview",z.GetCaughtGigantamaxed(species),v=>z.SetCaughtGigantamax(species,v));Flag("gmax1","Gigantamax variant obtained","Overview",z.GetCaughtGigantamax1(species),v=>z.SetCaughtGigantamax1(species,v));Number("battled","Times battled","Overview",z.GetBattledCount(species),uint.MaxValue,v=>z.SetBattledCount(species,v));
            var forms=Enumerable.Range(0,Math.Min(32,(int)sw.Personal[species].FormCount)).Select(f=>new Choice(f.ToString(),FormName(f))).ToArray();
            Pick("display.form","Form","Display",(int)z.GetFormDisplayed(species),forms,v=>z.SetFormDisplayed(species,(uint)v));Pick("display.gender","Gender","Display",(int)z.GetGenderDisplayed(species),DexGenders,v=>z.SetGenderDisplayed(species,(uint)v));Flag("display.shiny","Shiny","Display",z.GetDisplayShiny(species),v=>z.SetDisplayShiny(species,v));Flag("display.dynamax","Show Dynamax","Display",z.GetDisplayDynamaxInstead(species),v=>z.SetDisplayDynamaxInstead(species,v));
            foreach(var lang in DexLanguages)Flag("language."+lang.Id,lang.Name,"Languages",z.GetIsLanguageObtained(species,lang.Id),v=>z.SetIsLanguageObtained(species,lang.Id,v));
            foreach(var form in forms)for(int r=0;r<4;r++){byte f=byte.Parse(form.value);int region=r;Flag($"form.{f}.{r}",appearances[r],"Form: "+form.label,z.GetSeenRegion(species,f,r),v=>z.SetSeenRegion(species,f,region,v));}
        }
        if(sav is SAV9ZA za) {
            var z=za.Zukan;var e=z.GetEntry(species);
            Flag("new","Show as new","Overview",e.GetDisplayIsNew(),v=>z.GetEntry(species).SetDisplayIsNew(v));
            for(byte m=0;m<3;m++){byte index=m;Flag("mega."+index,$"Mega appearance {index+1}","Overview",e.GetIsSeenMega(index),v=>z.GetEntry(species).SetIsSeenMega(index,v));}
            Flag("alpha","Alpha seen","Overview",e.GetIsSeenAlpha(),v=>z.GetEntry(species).SetIsSeenAlpha(v));
            var forms=Enumerable.Range(0,Math.Min(32,(int)za.Personal[species].FormCount)).Select(f=>new Choice(f.ToString(),FormName(f))).ToArray();
            Pick("display.form","Form","Display",e.DisplayForm,forms,v=>{var entry=z.GetEntry(species);entry.DisplayForm=(byte)v;});
            Pick("display.gender","Gender","Display",(int)e.DisplayGender,ExtraEnums<DisplayGender9a>(),v=>{var entry=z.GetEntry(species);entry.DisplayGender=(DisplayGender9a)v;});
            Flag("display.shiny","Shiny","Display",e.GetDisplayIsShiny(),v=>z.GetEntry(species).SetDisplayIsShiny(v));
            foreach(var lang in DexLanguages)Flag("language."+lang.Id,lang.Name,"Languages",e.GetLanguageFlag(lang.Id),v=>z.GetEntry(species).SetLanguageFlag(lang.Id,v));
            for(byte gender=0;gender<3;gender++){byte g=gender;Flag("gender."+g,DexGenders[g].label,"Seen genders",e.GetIsGenderSeen(g),v=>z.GetEntry(species).SetIsGenderSeen(g,v));}
            foreach(var form in forms){byte f=byte.Parse(form.value);Flag($"form.{f}.seen","Seen","Form: "+form.label,e.GetIsFormSeen(f),v=>z.GetEntry(species).SetIsFormSeen(f,v));Flag($"form.{f}.caught","Caught","Form: "+form.label,e.GetIsFormCaught(f),v=>z.GetEntry(species).SetIsFormCaught(f,v));Flag($"form.{f}.shiny","Shiny seen","Form: "+form.label,e.GetIsShinySeen(f),v=>z.GetEntry(species).SetIsShinySeen(f,v));}
        }
        return result;
    }
    // The pinned core's upgrade property only recognizes the combined HGSS version.
    // Individual HG/SS identities use the same flags, without changing the saved ROM code.
    static int ReadDexUpgrade(SAV4 save)=>save is SAV4HGSS ? save.General[0x15ED]!=0?3:save.General[0x15EF]!=0?2:save.General[0x15EE]!=0&&(save.General[0x10D1]&8)!=0?1:0 : save.DexUpgraded;
    static void SetDexUpgrade(SAV4 save,int value){if(save is not SAV4HGSS){save.DexUpgraded=value;return;}save.General[0x15ED]=(byte)(value==3?1:0);save.General[0x15EF]=(byte)(value>=2?1:0);save.General[0x15EE]=(byte)(value>=1?1:0);save.General[0x10D1]=(byte)((save.General[0x10D1]&~8)|(value>=1?8:0));}
    DexRecordData ReadLegacyDex(int species)=>new(species,Species((ushort)species),GameInfo.GetVersionName(RequireSave().Version),LegacyDexFields(species).Select(x=>x.field).ToArray());
    void EditLegacyDex(JsonElement r){int species=N(r,"species");string key=S(r,"field");var entry=LegacyDexFields(species).FirstOrDefault(x=>x.field.id==key);if(entry.field==null)throw new Exception("That record is unavailable for this species.");entry.write(S(r,"value"));dirty=true;}
}
