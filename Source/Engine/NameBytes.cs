using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    static string NameBytesKey(PKM pk)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pk.Data.ToArray().Concat(pk.NicknameTrash.ToArray()).Concat(pk.OriginalTrainerTrash.ToArray()).Concat(pk.HandlingTrainerTrash.ToArray()).ToArray()));
    record NameBytesPage(int revision,string entityKey,Choice[] fields,string field,string text,string hex,int capacity,int maxLength,Choice[] characters);
    static Span<byte> NameBuffer(PKM pk,string field)=>field switch {"Nickname"=>pk.NicknameTrash,"OriginalTrainerName"=>pk.OriginalTrainerTrash,"HandlingTrainerName"=>pk.HandlingTrainerTrash,_=>throw new Exception("Choose a Pokémon name field.")};
    static int NameLimit(PKM pk,string field)=>field=="Nickname"?pk.MaxStringLengthNickname:pk.MaxStringLengthTrainer;
    static Choice[] NameCharacters(EntityContext context) {
        int[] modern=['…','♂','♀','♠','♣','♥','♦','★','◎','○','□','△','◇','♪','☀','☁','☂','☃'];
        int[] codes=context switch {EntityContext.Gen5=>Enumerable.Range(0x2460,37).Where(x=>x<0x2469||x>0x246B).ToArray(),EntityContext.Gen6 or EntityContext.Gen7 or EntityContext.Gen7b=>Enumerable.Range(0xE081,37).Where(x=>x<0xE08A||x>0xE08C).ToArray(),_ when !context.IsEraPreSwitch=>modern,_=>[]};
        string[] labels=["Face 1","Face 2","Face 3","Face 4","Up-right arrow","Down-right arrow","Sleeping","Multiply","Divide","Ellipsis","Male","Female","Spade","Club","Heart","Diamond","Star","Double circle","Circle","Square","Triangle","Diamond outline","Music note","Sun","Cloud","Umbrella","Snowman","Half face 1","Half face 2","Half face 3","Half face 4","Half up-right arrow","Half down-right arrow","Half sleeping"];
        return codes.Select((c,i)=>new Choice(((char)c).ToString(),$"{(context.IsEraPreSwitch?labels[i]:((char)c).ToString())} · U+{c:X4}")).ToArray();
    }
    static byte[] ParseNameHex(string text,int size) {
        string compact=string.Concat(text.Where(c=>!char.IsWhiteSpace(c)));if(compact.Length!=size*2)throw new Exception($"Enter exactly {size} bytes, using two hexadecimal digits per byte.");
        try{return Convert.FromHexString(compact);}catch(FormatException){throw new Exception("Use hexadecimal digits 0–9 and A–F.");}
    }
    object ReadNameBytes(JsonElement r) {
        var pk=RequireEntity();string field=S(r,"field");if(string.IsNullOrEmpty(field))field="Nickname";
        var bytes=NameBuffer(pk,field).ToArray();if(bytes.Length==0)throw new Exception("This format does not store that name.");
        var fields=new[]{new Choice("Nickname","Nickname"),new Choice("OriginalTrainerName","Original trainer"),new Choice("HandlingTrainerName","Handling trainer")}.Where(f=>NameBuffer(pk,f.value).Length>0).ToArray();
        return PreviewNameBytes(r,pk,field,bytes,NameLimit(pk,field),NameBytesKey(pk),fields);
    }
    object PreviewNameBytes(JsonElement r,PKM pk,string field,byte[] bytes,int limit,string key,Choice[] fields) {
        if(r.TryGetProperty("hex",out _)) {
            if(N(r,"revision")!=revision||S(r,"entityKey")!=key)throw new Exception("The Pokémon changed. Reopen the name editor.");
            bytes=ParseNameHex(S(r,"hex"),bytes.Length);
            string mode=S(r,"mode");
            if(mode=="text") {string text=S(r,"text");if(text.Length>limit)throw new Exception($"This field allows at most {limit} characters.");pk.SetString(bytes,text,text.Length,StringConverterOption.None);}
            else if(mode is "clear" or "layer") {
                string current=pk.GetString(bytes);var encoded=new byte[bytes.Length];int used=pk.SetString(encoded,current,current.Length,StringConverterOption.None);
                if(mode=="clear")bytes.AsSpan(used).Clear();
                else {
                    int species=N(r,"species"),language=N(r,"language"),generation=N(r,"generation");
                    if(species<1||species>=strings.specieslist.Length||language<1||language>10||language==6||generation<1||generation>9)throw new Exception("Choose a species, language and generation from the available ranges.");
                    string layer=SpeciesName.GetSpeciesNameGeneration((ushort)species,language,(byte)generation);if(string.IsNullOrEmpty(layer))throw new Exception("No species name is available for that language and generation.");
                    var temp=new byte[Math.Max(128,bytes.Length)];int length=pk.SetString(temp,layer,layer.Length,StringConverterOption.None);
                    if(length<=used)throw new Exception("The current text covers this layer. Choose a longer species name or shorter text.");
                    if(length>bytes.Length)throw new Exception("The species-name layer does not fit in this field.");
                    temp.AsSpan(used,length-used).CopyTo(bytes.AsSpan(used));
                }
            } else if(mode!="hex")throw new Exception("Choose an available text action.");
        }
        return new NameBytesPage(revision,key,fields,field,pk.GetString(bytes),Convert.ToHexString(bytes),bytes.Length,limit,NameCharacters(pk.Context));
    }
    void SetNameBytes(JsonElement r) {
        var original=RequireEntity();if(N(r,"revision")!=revision||S(r,"entityKey")!=NameBytesKey(original))throw new Exception("The Pokémon changed. Reopen the name editor.");
        string field=S(r,"field");var pk=original.Clone();var buffer=NameBuffer(pk,field);if(buffer.Length==0)throw new Exception("This format does not store that name.");
        ParseNameHex(S(r,"hex"),buffer.Length).CopyTo(buffer);pk.RefreshChecksum();entity=pk;pending=true;
    }
}
