using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    record DexRecordData(int species, string name, string edition, Field[] fields);
    static readonly (int Id,string Name)[] DexLanguages = [(1,"Japanese"),(2,"English"),(3,"French"),(4,"Italian"),(5,"German"),(7,"Spanish"),(8,"Korean"),(9,"Chinese (Simplified)"),(10,"Chinese (Traditional)")];
    static Choice[] DexStates => [new("0","Unknown"),new("1","Heard of"),new("2","Seen"),new("3","Caught")];
    static Choice[] DexGenders => [new("0","Male"),new("1","Female"),new("2","Genderless")];
    DexRecordData DexRecord(int id)
    {
        var sav=RequireSave();
        if(LegacyDex(sav))return ReadLegacyDex(id);
        if (sav is not (SAV8BS or SAV9SV)) throw new Exception("Detailed records are available for Brilliant Diamond, Shining Pearl, Scarlet, and Violet.");
        if (id<1 || id>sav.MaxSpeciesID) throw new Exception("Choose a species supported by this save.");
        var species=(ushort)id; var fields=new List<Field>();
        void Flag(string key,string label,string group,bool value) => fields.Add(new(key,label,group,value?"true":"false","bool",true,"",null,[]));
        void Pick(string key,string label,string group,int value,Choice[] choices) => fields.Add(new(key,label,group,value.ToString(),"enum",true,"",null,choices));
        var names=FormConverter.GetFormList(species,strings.Types,strings.forms,sav.Context);
        string FormName(int f) => f<names.Length && !string.IsNullOrWhiteSpace(names[f]) ? names[f] : f==0 ? "Normal" : $"Form {f}";
        if (sav is SAV8BS bd)
        {
            var dex=bd.Zukan;
            Pick("state","Entry status","Overview",(int)dex.GetState(species),DexStates);
            Flag("regional","Regional Pokédex unlocked","Pokédex unlocks",dex.HasRegionalDex);
            Flag("national","National Pokédex unlocked","Pokédex unlocks",dex.HasNationalDex);
            dex.GetGenderFlags(species,out var male,out var female,out var sm,out var sf);
            Flag("gender.0","Male","Seen appearances",male);Flag("gender.1","Female","Seen appearances",female);
            Flag("gender.2","Shiny male","Seen appearances",sm);Flag("gender.3","Shiny female","Seen appearances",sf);
            foreach(var lang in DexLanguages) Flag($"language.{lang.Id}",lang.Name,"Languages obtained",dex.GetLanguageFlag(species,lang.Id));
            for(byte f=0;f<Zukan8b.GetFormCount(species);f++) {
                Flag($"form.{f}.normal","Regular","Form: "+FormName(f),dex.GetHasFormFlag(species,f,false));
                Flag($"form.{f}.shiny","Shiny","Form: "+FormName(f),dex.GetHasFormFlag(species,f,true));
            }
            return new(id,Species(species),"Sinnoh",fields.ToArray());
        }
        var sv=(SAV9SV)sav; var z=sv.Zukan; var updated=z.GetRevision()!=0;
        var forms=Enumerable.Range(0,Math.Min(32,(int)sv.Personal[species].FormCount))
            .Where(f=>sv.Personal.GetFormEntry(species,(byte)f).IsPresentInGame)
            .Select(f=>new Choice(f.ToString(),FormName(f))).ToArray();
        if (forms.Length==0) throw new Exception("This species has no forms in this game's Pokédex.");
        if (!updated) {
            var old=z.DexPaldea.Get(species);
            Pick("state","Entry status","Overview",(int)old.GetState(),DexStates);
            Flag("new","Show as new","Overview",old.GetDisplayIsNew());
            Flag("model.shiny","Shiny obtained","Seen appearances",old.GetSeenIsShiny());
            Flag("different","Different gender appearance","Display: Paldea",old.GetDisplayGenderIsDifferent());
            Pick("display.Paldea.form","Form","Display: Paldea",(int)old.GetDisplayForm(),forms);
            Pick("display.Paldea.gender","Gender","Display: Paldea",(int)old.GetDisplayGender(),DexGenders);
            Flag("display.Paldea.shiny","Shiny","Display: Paldea",old.GetDisplayIsShiny());
            for(byte g=0;g<3;g++) Flag($"gender.{g}",DexGenders[g].label,"Seen appearances",old.GetIsGenderSeen(g));
            foreach(var lang in DexLanguages) Flag($"language.{lang.Id}",lang.Name,"Languages obtained",old.GetLanguageFlag(lang.Id));
            foreach(var form in forms) Flag($"form.{form.value}.seen",form.label,"Forms obtained",old.GetIsFormSeen(byte.Parse(form.value)));
        } else {
            var entry=z.DexKitakami.Get(species);
            for(byte g=0;g<3;g++) Flag($"gender.{g}",DexGenders[g].label,"Seen appearances",entry.GetIsGenderSeen(g));
            Flag("model.normal","Regular model","Seen appearances",entry.GetIsModelSeen(false));
            Flag("model.shiny","Shiny model","Seen appearances",entry.GetIsModelSeen(true));
            foreach(var lang in DexLanguages) Flag($"language.{lang.Id}",lang.Name,"Languages obtained",entry.GetLanguageFlag(lang.Id));
            foreach(var form in forms) {
                var f=byte.Parse(form.value);var group="Form: "+form.label;
                Flag($"form.{f}.heard","Heard of",group,entry.GetHeardForm(f));Flag($"form.{f}.seen","Seen",group,entry.GetSeenForm(f));
                Flag($"form.{f}.obtained","Caught",group,entry.GetObtainedForm(f));Flag($"form.{f}.checked","Entry viewed",group,entry.GetCheckedForm(f));
            }
            foreach(var region in new[]{"Paldea","Kitakami","Blueberry"}) {
                var regional=forms.Where(f=> {var pi=sv.Personal.GetFormEntry(species,byte.Parse(f.value));return region=="Paldea" ? pi.DexPaldea!=0 : region=="Kitakami" ? pi.DexKitakami!=0 : pi.DexBlueberry!=0;}).ToArray();
                if(regional.Length==0 || (region=="Kitakami" && sv.SaveRevision<1) || (region=="Blueberry" && sv.SaveRevision<2)) continue;
                int f=region=="Paldea" ? entry.DisplayedPaldeaForm : region=="Kitakami" ? entry.DisplayedKitakamiForm : entry.DisplayedBlueberryForm;
                int g=region=="Paldea" ? entry.DisplayedPaldeaGender : region=="Kitakami" ? entry.DisplayedKitakamiGender : entry.DisplayedBlueberryGender;
                bool shiny=(region=="Paldea" ? entry.DisplayedPaldeaShiny : region=="Kitakami" ? entry.DisplayedKitakamiShiny : entry.DisplayedBlueberryShiny)!=0;
                Pick($"display.{region}.form","Form","Display: "+region,f,regional);Pick($"display.{region}.gender","Gender","Display: "+region,g,DexGenders);Flag($"display.{region}.shiny","Shiny","Display: "+region,shiny);
            }
        }
        return new(id,Species(species),updated ? "Paldea, Kitakami & Blueberry" : "Paldea (original format)",fields.ToArray());
    }
    void EditDexGroup(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The Pokédex changed. Reopen its details.");
        int species=N(r,"species");var group=S(r,"group");var fields=DexRecord(species).fields.Where(f=>f.group==group && f.kind=="bool" && f.editable).ToArray();
        if(fields.Length==0 || group=="Overview" || group.StartsWith("Display") || group.Contains("unlock",StringComparison.OrdinalIgnoreCase))throw new Exception("Choose a form, appearance or language group with stored flags.");
        foreach(var field in fields){using var request=JsonDocument.Parse(JsonSerializer.Serialize(new {species,field=field.id,value=B(r,"value")?"true":"false"}));EditDexRecord(request.RootElement);}
    }
    void EditDexRecord(JsonElement r)
    {
        if(LegacyDex(save)){EditLegacyDex(r);return;}
        int speciesId=N(r,"species");var data=DexRecord(speciesId);var key=S(r,"field");var value=S(r,"value");
        var field=data.fields.FirstOrDefault(f=>f.id==key) ?? throw new Exception("This detail is not stored for this species or save revision.");
        bool flag=false;int number=0;
        if(field.kind=="bool") {if(value is not ("true" or "false")) throw new Exception("Choose on or off.");flag=value=="true";}
        else {if(!field.choices.Any(c=>c.value==value)) throw new Exception("Choose one of the available values.");number=int.Parse(value);}
        var species=(ushort)speciesId;var parts=key.Split('.');
        if(save is SAV8BS bd) {
            var z=bd.Zukan;
            switch(parts[0]) {
                case "state":z.SetState(species,(ZukanState8b)number);break;
                case "regional":z.HasRegionalDex=flag;break;
                case "national":z.HasNationalDex=flag;break;
                case "language":z.SetLanguageFlag(species,int.Parse(parts[1]),flag);break;
                case "form":z.SetHasFormFlag(species,byte.Parse(parts[1]),parts[2]=="shiny",flag);break;
                case "gender":z.GetGenderFlags(species,out var m,out var f,out var sm,out var sf);var flags=new[]{m,f,sm,sf};flags[int.Parse(parts[1])]=flag;z.SetGenderFlags(species,flags[0],flags[1],flags[2],flags[3]);break;
            }
        } else {
            var z=((SAV9SV)save!).Zukan;
            if(z.GetRevision()==0) {
                var e=z.DexPaldea.Get(species);
                switch(parts[0]) {
                    case "state":e.SetState((uint)number);break;
                    case "new":e.SetDisplayIsNew(flag);break;
                    case "different":e.SetDisplayGenderIsDifferent(flag);break;
                    case "model":e.SetSeenIsShiny(flag);break;
                    case "gender":e.SetIsGenderSeen(byte.Parse(parts[1]),flag);break;
                    case "language":e.SetLanguageFlag(int.Parse(parts[1]),flag);break;
                    case "form":e.SetIsFormSeen(byte.Parse(parts[1]),flag);break;
                    case "display":if(parts[2]=="form")e.SetDisplayForm((uint)number);else if(parts[2]=="gender")e.SetDisplayGender(number);else e.SetDisplayIsShiny(flag);break;
                }
            } else {
                var e=z.DexKitakami.Get(species);
                switch(parts[0]) {
                    case "gender":e.SetIsGenderSeen(byte.Parse(parts[1]),flag);break;
                    case "model":e.SetIsModelSeen(parts[1]=="shiny",flag);break;
                    case "language":e.SetLanguageFlag(int.Parse(parts[1]),flag);break;
                    case "form":var f=byte.Parse(parts[1]);switch(parts[2]){case "heard":e.SetHeardForm(f,flag);break;case "seen":e.SetSeenForm(f,flag);break;case "obtained":e.SetObtainedForm(f,flag);break;case "checked":e.SetCheckedForm(f,flag);break;}break;
                    case "display":
                        var v=(byte)(parts[2]=="shiny" ? (flag?1:0) : number);
                        if(parts[1]=="Paldea") {if(parts[2]=="form")e.DisplayedPaldeaForm=v;else if(parts[2]=="gender")e.DisplayedPaldeaGender=v;else e.DisplayedPaldeaShiny=v;}
                        else if(parts[1]=="Kitakami") {if(parts[2]=="form")e.DisplayedKitakamiForm=v;else if(parts[2]=="gender")e.DisplayedKitakamiGender=v;else e.DisplayedKitakamiShiny=v;}
                        else {if(parts[2]=="form")e.DisplayedBlueberryForm=v;else if(parts[2]=="gender")e.DisplayedBlueberryGender=v;else e.DisplayedBlueberryShiny=v;}break;
                }
            }
        }
        dirty=true;
    }
}
