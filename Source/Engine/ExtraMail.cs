using PKHeX.Core;

sealed partial class EditorSession {
    static int[] MailItems(SaveFile s)=>s.Generation switch{2=>[158,181,182,183,184,185,186,187,188,189],3=>Enumerable.Range(121,12).ToArray(),_=>Enumerable.Range(137,12).ToArray()};
    static int MailPartyCount(SaveFile s)=>s is SAV2Stadium?SAV2Stadium.MailboxHeldMailCount:s.Generation<=3?6:s.PartyCount;
    static int MailStoredCount(SaveFile s)=>s is SAV2Stadium?SAV2Stadium.MailboxMailCount:s.Generation<=3?10:20;
    static int MailBoxSizeOffset(SaveFile s)=>s is SAV2Stadium?Mail2.GetMailboxOffsetStadium2(s.Language):Mail2.GetMailboxOffset(s.Language);
    static int MailOffset2(SaveFile s,int index){int party=MailPartyCount(s),size=Mail2.GetMailSize(s.Language);return index<party?(s is SAV2Stadium?SAV2Stadium.MailboxHeldBlockOffset(s.Language)+2:0x600)+index*size:MailBoxSizeOffset(s)+1+(index-party)*size;}
    static MailDetail MailAt(SaveFile s,int index){int party=MailPartyCount(s);return s switch{
        SAV2 g=>new Mail2(g,index),SAV2Stadium g=>new Mail2(g,index),SAV3 g=>g.LargeBlock.GetMail(index),
        SAV4 g=>index<party?new Mail4(((PK4)g.GetPartySlotAtIndex(index)).HeldMail.ToArray()):g.GetMail(index-party),
        SAV5 g=>index<party?new Mail5(((PK5)g.GetPartySlotAtIndex(index)).HeldMail.ToArray()):g.GetMail(index-party),_=>throw new Exception("This save does not store mail.")};}
    static byte[] MailBytes(SaveFile s,int index){int party=MailPartyCount(s);if(s.Generation==2)return s.Data.Slice(MailOffset2(s,index),Mail2.GetMailSize(s.Language)).ToArray();if(s is SAV3 g){var bytes=new byte[Mail3.SIZE];g.LargeBlock.GetMail(index).CopyTo(bytes);return bytes;}if(s is SAV4 four)return index<party?((PK4)four.GetPartySlotAtIndex(index)).HeldMail.ToArray():four.GetMailData(four.GetMailOffset(index-party));if(s is SAV5 five)return index<party?((PK5)five.GetPartySlotAtIndex(index)).HeldMail.ToArray():five.GetMailData(SAV5.GetMailOffset(index-party));throw new Exception("Unsupported mail.");}
    static void StoreMail(SaveFile s,int index,MailDetail mail){int party=MailPartyCount(s);if(s is SAV3 three){three.LargeBlock.SetMail(index,(Mail3)mail);return;}if(s.Generation>=4&&index<party){var pk=s.GetPartySlotAtIndex(index);if(pk is PK4 four)mail.CopyTo(four);else mail.CopyTo((PK5)pk);s.SetPartySlotAtIndex(pk,index,EntityImportSettings.None);return;}mail.CopyTo(s);}
    static void StoreMailBytes(SaveFile s,int index,byte[] bytes){int party=MailPartyCount(s);if(s.Generation==2){bytes.CopyTo(s.Data[MailOffset2(s,index)..]);return;}if(s is SAV3 three){three.LargeBlock.SetMail(index,new Mail3(bytes,0));return;}if(s is SAV4 four){StoreMail(s,index,new Mail4(bytes,index<party?-1:four.GetMailOffset(index-party)));return;}StoreMail(s,index,new Mail5(bytes,index<party?-1:SAV5.GetMailOffset(index-party)));}
    static void SyncMail2(SaveFile s){if(s is not SAV2)return;int size=Mail2.GetMailSize(s.Language),held=6*size; s.Data.Slice(0x600,held).CopyTo(s.Data.Slice(0x600+held,held));int offset=MailBoxSizeOffset(s),length=10*size+1;s.Data.Slice(offset,length).CopyTo(s.Data.Slice(offset+length,length));}
    ExtraPage ReadMail(ExtraTool tool,string? selected){
        var s=RequireSave();int party=MailPartyCount(s),count=party+MailStoredCount(s);var rows=new List<ExtraRow>();var itemIDs=MailItems(s);var itemNames=strings.GetItemStrings(s.Context,s.Version);
        Choice[] types=itemIDs.Select((n,i)=>new Choice((s.Generation<=3?n:i).ToString(),itemNames[n])).Prepend(new((s.Generation<=3?0:255).ToString(),"Empty")).ToArray();
        if(s.Generation==2)rows.Add(ER("capacity","Mailbox size",[EV("Count","Letters shown",s.Data[MailBoxSizeOffset(s)],MailStoredCount(s))],"The first letters up to this count appear in-game. Extra stored letters remain available here.") with {category="Mailbox"});
        if(s.HasParty)for(int i=0;i<s.PartyCount;i++){var pk=s.GetPartySlotAtIndex(i);var fields=new List<ExtraValue>{EV("HeldItem","Held item",pk.HeldItem,choices:itemIDs.Select(n=>new Choice(n.ToString(),itemNames[n])).Prepend(new("0","None")).Append(new(pk.HeldItem.ToString(),itemNames[Math.Clamp(pk.HeldItem,0,itemNames.Length-1)])).DistinctBy(c=>c.value).ToArray())};if(pk is PK3 p3)fields.Add(EV("HeldMailID","Letter slot",p3.HeldMailID,5,-1));int slot=pk is PK3 pk3?pk3.HeldMailID:i;bool isMail=itemIDs.Contains(pk.HeldItem);string detail=isMail&& (slot<0||slot>=party||MailAt(s,slot).IsEmpty!=false)?"The held mail item has no matching letter. Choose its slot and stationery.":!isMail&&slot>=0&&slot<party&&MailAt(s,slot).IsEmpty==false?"A stored letter is present, but this Pokémon is not holding a mail item.":"Held item and mail association.";rows.Add(ER("pokemon:"+i,$"Party {i+1} · "+Species(pk.Species),fields.ToArray(),detail,Sprite(pk)) with{category="Party"});}
        for(int i=0;i<count;i++){var m=MailAt(s,i);var fields=new List<ExtraValue>();string id="letter:"+i;bool show=selected==id;string author=m.AuthorName;bool? empty=m.IsEmpty;
            if(show){fields.AddRange([EP(m,"MailType","Stationery",choices:types),ET("AuthorName","From",author,s is SAV2 {Korean:true}?10:s.Generation<=3&&(s.Language==1)?5:7),EP(m,"AuthorTID","Trainer ID",65535)]);int[] languages=s.Generation==2?((s.Language==1)?[1]:s is SAV2 {Korean:true}?[8]:[2,3,4,5,7]):s.Generation==3?[1,2]:[1,2,3,4,5,7,8];fields.Add(EP(m,"AuthorLanguage","Language",choices:languages.Select(l=>new Choice(l.ToString(),Label(((LanguageID)l).ToString()))).ToArray()));
                if(s.Generation>2)fields.Add(EP(m,"AuthorSID","Secret ID",65535));
                if(s.Generation<=3)fields.Add(EV("Species","Portrait Pokémon",s.Generation==3?SpeciesConverter.GetNational3(m.AppearPKM):m.AppearPKM,choices:SpeciesChoices(s.MaxSpeciesID).Where(c=>s.Generation!=3||c.value!="0").ToArray()));
                if(m is Mail2){fields.AddRange([ET("Line1","First line",m.GetMessage(false),16) with{group="Message"},ET("Line2","Second line",m.GetMessage(true),16) with{group="Message"},EV("UserEntered","Player-written layout",m.UserEntered) with{group="Message"}]);}
                else {if(s.Generation>=4){fields.Add(EP(m,"AuthorVersion","Origin game",choices:Enum.GetValues<GameVersion>().Where(v=>v.IsValidSavedVersion()&&v.Context==s.Context).Select(v=>new Choice(((int)v).ToString(),GameInfo.GetVersionName(v))).ToArray()));fields.Add(EV("Gender","Author gender",m.AuthorGender&1,choices:[new("0","Male"),new("1","Female")]));}for(int y=0;y<3;y++)for(int x=0;x<(s.Generation==3?3:4);x++)fields.Add(EV($"Word{y}{x}",$"Line {y+1} · phrase code {x+1}",m.GetMessage(y,x),65535) with{group="Message"});}
                if(m is Mail4 four)for(int n=0;n<3;n++){int raw=four.GetAppearSpecies(n);fields.Add(EV("Portrait"+n,$"Portrait {n+1}",raw==65535?0:Math.Max(0,raw-7),choices:SpeciesChoices(s.MaxSpeciesID)) with{group="Portraits"});}
                if(m is Mail5 five){fields.Add(EP(five,"MessageEnding","Message ending",65535) with{group="Message"});for(int n=0;n<3;n++)fields.Add(EV("Misc"+n,$"Stored value {n+1}",five.GetMisc(n),65535) with{group="Advanced"});}
            }
            int start=i<party?0:party,end=i<party?party:count;var actions=new List<Choice>{new("clearMail","Clear Letter"),new("detachMail","Clear Letter and Detach Held Item")};if(s.Generation<=3&&s is not SAV2Stadium){if(i>start)actions.Add(new("upMail","Move Up"));if(i+1<end)actions.Add(new("downMail","Move Down"));}if(i>=party)actions.RemoveAll(a=>a.value=="detachMail");
            rows.Add(ER(id,$"{(i<party?"Held letter":"Letter")} {(i<party?i+1:i-party+1):00} · "+(empty==true?"Empty":string.IsNullOrWhiteSpace(author)?"No author":author),fields.ToArray(),empty==null?"Unrecognized stationery value · inspect before editing":types.FirstOrDefault(t=>t.value==m.MailType.ToString())?.label??"Stored mail",actions:actions.ToArray()) with{category=i<party?"Held letters":"Mailbox"});
        }
        return new("mail",revision,tool,rows.ToArray(),[]);
    }
    void EditMail(string id,string mode,Dictionary<string,string> edits){
        var s=RequireSave();var parts=id.Split(':');int index=parts.Length>1?int.Parse(parts[1]):-1;
        if(id=="capacity"){s.Data[MailBoxSizeOffset(s)]=byte.Parse(edits["Count"]);SyncMail2(s);return;}
        if(parts[0]=="pokemon"){var pk=s.GetPartySlotAtIndex(index);foreach(var(k,v) in edits)SetProperty(pk,k,v);s.SetPartySlotAtIndex(pk,index,EntityImportSettings.None);return;}
        if(mode is "upMail" or "downMail"){int other=index+(mode=="upMail"?-1:1);var a=MailBytes(s,index);var b=MailBytes(s,other);StoreMailBytes(s,index,b);StoreMailBytes(s,other,a);SyncMail2(s);return;}
        var m=MailAt(s,index);
        if(mode is "clearMail" or "detachMail"){
            if(m is Mail4 four){four.SetBlank((byte)s.Language,(byte)s.Version);four.MailType=255;StoreMail(s,index,four);}else{if(m is Mail5 five)five.SetBlank((byte)s.Language,(byte)s.Version);else m.SetBlank();StoreMail(s,index,m);}
            if(mode=="detachMail")for(int i=0;i<s.PartyCount;i++){var pk=s.GetPartySlotAtIndex(i);int held=pk is PK3 three?three.HeldMailID:i;if(held!=index||!MailItems(s).Contains(pk.HeldItem))continue;pk.HeldItem=0;if(pk is PK3 p3)p3.HeldMailID=-1;s.SetPartySlotAtIndex(pk,i,EntityImportSettings.None);}
            SyncMail2(s);return;
        }
        // Read strings once: Gen 2's original English mail decoder works in-place on this detached copy.
        var readable=MailAt(s,index);string author=readable.AuthorName,line1=readable is Mail2?readable.GetMessage(false):"",line2=readable is Mail2?readable.GetMessage(true):"";bool user=m.UserEntered;
        if(edits.Remove("AuthorLanguage",out var language)) {m.AuthorLanguage=byte.Parse(language);if(!edits.ContainsKey("AuthorName"))edits["AuthorName"]=author;if(m is Mail2){edits.TryAdd("Line1",line1);edits.TryAdd("Line2",line2);}}
        if(m is Mail2 && (edits.ContainsKey("Line1")||edits.ContainsKey("Line2")||edits.ContainsKey("UserEntered"))){m.SetMessage(edits.GetValueOrDefault("Line1",line1),edits.GetValueOrDefault("Line2",line2),edits.TryGetValue("UserEntered",out var u)?bool.Parse(u):user);edits.Remove("Line1");edits.Remove("Line2");edits.Remove("UserEntered");}
        foreach(var(k,v) in edits){if(k=="Species")m.AppearPKM=s.Generation==3?SpeciesConverter.GetInternal3(ushort.Parse(v)):ushort.Parse(v);else if(k=="Gender")m.AuthorGender=(byte)((m.AuthorGender&0xFE)|byte.Parse(v));else if(k.StartsWith("Word"))m.SetMessage(k[4]-'0',k[5]-'0',ushort.Parse(v));else if(k.StartsWith("Portrait"))((Mail4)m).SetAppearSpecies(int.Parse(k[8..]),ushort.Parse(v)==0?(ushort)0:(ushort)(ushort.Parse(v)+7));else if(k.StartsWith("Misc"))((Mail5)m).SetMisc(int.Parse(k[4..]),int.Parse(v));else SetProperty(m,k,v);}
        StoreMail(s,index,m);SyncMail2(s);
    }
}
