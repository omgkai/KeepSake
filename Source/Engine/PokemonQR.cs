using PKHeX.Core;
using System.Text.Json;
sealed partial class EditorSession
{
    object PokemonQR()
    {
        var pk=RequireEntity();if(pk.Species==0)throw new Exception("Choose a Pokémon to share.");
        var message=QRMessageUtil.GetMessage(pk);
        return new {payload=Convert.ToBase64String(message.Select(c=>checked((byte)c)).ToArray()),lines=pk.GetQRLines(),format=pk.Extension};
    }
    void ImportPokemonQR(JsonElement r)
    {
        string encoded=S(r,"payload");if(encoded.Length is <1 or >24000)throw new Exception("The QR payload is too large or empty.");
        var bytes=Convert.FromBase64String(encoded);var message=new string(bytes.Select(b=>(char)b).ToArray());
        var pk=QRMessageUtil.GetPKM(message,save?.Context ?? entity?.Context ?? EntityContext.None) ?? throw new Exception("This QR code does not contain a supported Pokémon.");
        if(pk.Species==0 || !pk.ChecksumValid)throw new Exception("The QR Pokémon is empty or its checksum is invalid.");
        if(save is {} sav) {
            if(pk.GetType()!=sav.PKMType)pk=EntityConverter.ConvertToType(pk,sav.PKMType,out _) ?? throw new Exception("This QR Pokémon cannot transfer to the loaded game.");
            if(!sav.Personal.IsPresentInGame(pk.Species,pk.Form))throw new Exception("This Pokémon or form is unavailable in the loaded game.");
        }
        entity=pk;entitySourcePath=null;pending=true;
    }
}

sealed partial class EditorSession
{
    object GiftQR(JsonElement r)
    {
        var gift = Gift(N(r,"id")) as DataMysteryGift ?? throw new Exception("This event has no card data to encode.");
        var message = QRMessageUtil.GetMessage(gift);
        if (message.Length > 2300) throw new Exception("This card is too large for a QR code. Use Export Card instead.");
        return new { payload = Convert.ToBase64String(System.Text.Encoding.Latin1.GetBytes(message)), lines = new[] { gift.CardTitle, $"Card {gift.CardID} · {gift.Extension}" }, format = gift.Extension };
    }
    object ImportGiftQR(JsonElement r)
    {
        var encoded = S(r,"payload");
        if (encoded.Length is <1 or >24000) throw new Exception("The QR payload is too large or empty.");
        var message = System.Text.Encoding.Latin1.GetString(Convert.FromBase64String(encoded));
        var separator = message.IndexOf('#');
        if (separator < 0) throw new Exception("This is not a PKHeX gift-card QR code.");
        var bytes = Convert.FromBase64String(message[(separator+1)..]);
        var extension = "." + S(r,"format").TrimStart('.');
        if (!giftExtensions.Contains(extension)) throw new Exception("Choose the card’s original file format.");
        var gift = MysteryGift.GetMysteryGift(bytes, extension) ?? throw new Exception("The QR card does not match the selected format.");
        if ((!gift.IsEntity && !gift.IsItem) || (gift.IsEntity && (gift.Species == 0 || gift.Species > 1025))) throw new Exception("This QR card contains unsupported gift data.");
        _ = GiftRow(gift,0);
        var hash = extension.ToLowerInvariant() + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        if (!localGifts.Values.Any(x => x.Hash == hash))
        {
            if (nextGiftID < 0) nextGiftID = giftDatabase.Value.Length;
            if (localGifts.Count >= 5000) throw new Exception("The session gift library is full.");
            localGifts.Add(nextGiftID++, new(gift,"QR card" + extension,hash));
        }
        return Gifts();
    }
}
