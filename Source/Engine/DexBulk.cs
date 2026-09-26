using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    object DexBulkOptions() {
        var s=RequireSave();bool extended=s is SAV4 or SAV5 or SAV6 or SAV7 or SAV7b or SAV8SWSH or SAV8BS or SAV9SV or SAV9ZA;
        var actions=new List<Choice>{new("seen","Mark All Seen"),new("caught","Mark All Caught"),new("unseen","Clear Seen Records"),new("uncaught","Clear Caught")};
        if(extended)actions.Add(new("complete","Complete Forms and Languages"));
        if(s is SAV5 or SAV6){actions.Add(new("forms","See All Forms"));actions.Add(new("firstForms","Mark First Forms Seen"));actions.Add(new("clearForms","Clear Form Records"));}
        return new {revision,actions,shiny=extended&&s is not SAV4,allLanguages=s is SAV5 or SAV6,entries=extended};
    }
    static void BulkZukan<T>(ZukanBase<T> z,string action,bool shiny,ushort species) where T:SaveFile {
        if(species!=0){if(action=="complete")z.SetDexEntryAll(species,shiny);else if(action=="unseen")z.ClearDexEntryAll(species);else throw new Exception("Choose Complete or Clear for an individual entry.");return;}
        switch(action){case "seen":z.SeenAll(shiny);break;case "caught":z.CaughtAll(shiny);break;case "unseen":z.SeenNone();break;case "uncaught":z.CaughtNone();break;case "complete":z.CompleteDex(shiny);break;default:throw new Exception("Choose a listed Pokédex action.");}
    }
    void EditDexBulk(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Reopen these Pokédex actions.");
        var s=RequireSave();string action=S(r,"action");bool shiny=B(r,"shiny"),languages=B(r,"allLanguages");int selected=N(r,"species");
        if(!SupportsDex(s))throw new Exception("This save has no supported Pokédex.");
        if(selected<0||selected>s.MaxSpeciesID||selected!=0&&!DexSpeciesAvailable(s,(ushort)selected))throw new Exception("Choose a species in this game’s Pokédex.");
        ushort species=(ushort)selected;
        if(species!=0&&action is not ("complete" or "unseen"))throw new Exception("Choose Complete or Clear for one entry.");
        switch(s) {
            case SAV4 x:BulkZukan(x.Dex,action,shiny,species);break;
            case SAV7 x:BulkZukan(x.Zukan,action,shiny,species);break;
            case SAV7b x:BulkZukan(x.Zukan,action,shiny,species);break;
            case SAV8SWSH x:BulkZukan(x.Zukan,action,shiny,species);break;
            case SAV8BS x:BulkZukan(x.Zukan,action,shiny,species);break;
            case SAV9SV x:BulkZukan(x.Zukan,action,shiny,species);break;
            case SAV9ZA x:BulkZukan(x.Zukan,action,shiny,species);break;
            case SAV5 x:
                if(species!=0)x.Zukan.GiveAll(species,action=="complete",shiny,(LanguageID)s.Language,languages);
                else {switch(action){case "seen":x.Zukan.SeenAll(shiny);break;case "caught":x.Zukan.CaughtAll((LanguageID)s.Language,languages);break;case "complete":x.Zukan.SeenAll(shiny);x.Zukan.CaughtAll((LanguageID)s.Language,languages);break;case "unseen":x.Zukan.SeenNone();break;case "uncaught":x.Zukan.CaughtNone();break;case "forms":x.Zukan.SetFormsSeen(shiny);break;case "firstForms":x.Zukan.SetFormsSeen1(shiny);break;case "clearForms":x.Zukan.ClearFormSeen();break;default:throw new Exception("Choose a listed Pokédex action.");}}break;
            case SAV6 x:
                var z=x is SAV6XY xy?(Zukan6)xy.Zukan:((SAV6AO)x).Zukan;
                if(species!=0)z.GiveAll(species,action=="complete",shiny,(LanguageID)s.Language,languages);
                else {switch(action){case "seen":z.SeenAll(shiny);break;case "caught":z.CaughtAll((LanguageID)s.Language,languages);break;case "complete":z.SeenAll(shiny);z.CaughtAll((LanguageID)s.Language,languages);break;case "unseen":z.SeenNone();break;case "uncaught":z.CaughtNone();break;case "forms":z.SetFormsSeen(shiny);break;case "firstForms":z.SetFormsSeen1(shiny);break;case "clearForms":z.ClearFormSeen();break;default:throw new Exception("Choose a listed Pokédex action.");}}break;
            default:
                if(species!=0||action is not ("seen" or "caught" or "unseen" or "uncaught"))throw new Exception("This game supports bulk seen/caught flags only.");
                for(ushort i=1;i<=s.MaxSpeciesID;i++)if(DexSpeciesAvailable(s,i))SetDexFlags(s,i,action!="unseen"&&(action!="uncaught"||s.GetSeen(i)),action=="caught"||(action=="seen"&&s.GetCaught(i)));
                break;
        }
        dirty=true;
    }
}
