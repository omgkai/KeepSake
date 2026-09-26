using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    static readonly Lazy<MysteryGift[]> giftDatabase=new(()=>EncounterEvent.GetAllEvents().ToArray());
    MysteryGift Gift(int id) => id>=0 && id<giftDatabase.Value.Length ? giftDatabase.Value[id] : localGifts.TryGetValue(id,out var entry) ? entry.Gift : throw new Exception("This gift selection is no longer available. Choose a card from the current library.");
    object Gifts() => giftDatabase.Value.Select((g,i)=>GiftRow(g,i)).Concat(localGifts.Select(x=>GiftRow(x.Value.Gift,x.Key,"Folder",Path.GetFileName(x.Value.Path)))).ToArray();
    void PrepareGift(int id)
    {
        var sav=RequireSave();var gift=Gift(id);
        if(!gift.IsEntity) throw new Exception("This gift contains items, not a Pokémon.");
        var source=gift is DataMysteryGift data ? data.Clone() : gift;
        // These database restrictions live outside the serialized card bytes.
        if(gift is PGF p && source is PGF pc){pc.RestrictLanguage=p.RestrictLanguage;pc.RestrictVersion=p.RestrictVersion;}
        if(gift is WC6 w6 && source is WC6 c6){c6.RestrictLanguage=w6.RestrictLanguage;c6.RestrictVersion=w6.RestrictVersion;if(c6.Language==0 && c6.RestrictLanguage!=0)c6.Language=c6.RestrictLanguage;}
        if(gift is WC7 w7 && source is WC7 c7){c7.RestrictLanguage=w7.RestrictLanguage;c7.RestrictVersion=w7.RestrictVersion;if(c7.Language==0 && c7.RestrictLanguage!=0)c7.Language=c7.RestrictLanguage;}
        var pk=source.ConvertToPKM(sav);
        if(pk.GetType()!=sav.PKMType) pk=EntityConverter.ConvertToType(pk,sav.PKMType,out _) ?? throw new Exception("This event Pokémon cannot be converted to the open save format.");
        if(pk.Species>sav.MaxSpeciesID) throw new Exception("This Pokémon is not supported by this save format.");
        pk.RefreshChecksum();entity=pk;entitySourcePath=null;pending=true;
    }
    object ExportGift(JsonElement r)
    {
        var gift=Gift(N(r,"id")) as DataMysteryGift ?? throw new Exception("This event has no card file.");
        var path=ExportPath(S(r,"path"),sourcePath ?? entitySourcePath);
        if(giftSourcePaths.Contains(path))throw new Exception("Export a copy to a different path; do not overwrite a library source card.");
        AtomicWrite(path,gift.Write().ToArray());
        return new {count=1,path};
    }
}
