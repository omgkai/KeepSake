using System.Globalization;
using PKHeX.Core;

sealed partial class EditorSession
{
    ExtraPage ReadTraining6(ExtraTool tool)
    {
        var s=(ISaveBlock6Main)RequireSave();var training=s.SuperTrain;var rows=new List<ExtraRow>();
        var species=Enumerable.Range(0,722).Select(i=>new Choice(i.ToString(),Species((ushort)i))).ToArray();
        for(int i=0;i<32;i++) {
            var fields=new List<ExtraValue>{EV("Unlocked","Stage unlocked",training.GetIsRegimenUnlocked(i)) with {group="Stage"}};
            for(int rank=1;rank<=2;rank++) {
                string group=rank==1?"Best record":"Second record";var holder=rank==1?training.GetHolder1(i):training.GetHolder2(i);float time=rank==1?training.GetTime1(i):training.GetTime2(i);
                fields.Add(new("Time"+rank,"Time in seconds",time.ToString("R",CultureInfo.InvariantCulture),"float","0",float.MaxValue.ToString("R",CultureInfo.InvariantCulture),[],group));
                fields.Add(EV("Species"+rank,"Pokémon",holder.Species,choices:species) with {group=group});
                fields.Add(EV("Form"+rank,"Form",holder.Form,255) with {group=group});
                fields.Add(EV("Gender"+rank,"Gender",holder.Gender,choices:[new("0","Boy"),new("1","Girl"),new("2","Genderless")]) with {group=group});
            }
            rows.Add(ER("stage:"+i,$"{i+1:00} · {strings.trainingstage[i]}",fields.ToArray(),"Two record holders and their times. Decimal seconds are preserved.",actions:[new("clearRecords","Clear Both Records")]));
        }
        var bags=strings.trainingbags.Select((name,i)=>new Choice(i.ToString(),i==0?"Empty":name)).Where(c=>!string.IsNullOrWhiteSpace(c.label)).ToArray();
        rows.Add(ER("bags","Training bag pouch",[],"12 slots. Removing a bag packs the remaining bags in order.",actions:[new("clearBags","Empty Bag Pouch")]));
        for(int i=0;i<12;i++) {byte bag=training.GetBag(i);rows.Add(ER("bag:"+i,$"Bag slot {i+1}",[EV("Bag","Training bag",bag,choices:bags)],bag<strings.trainingbags.Length?strings.trainingbags[bag]:$"Stored bag {bag}"));}
        for(int i=0;i<6;i++)rows.Add(ER("distribution:"+i,$"Distribution stage {i+1}",[EV("Unlocked","Unlocked",training.GetIsDistributionUnlocked(i))],"Special distribution regimen."));
        return new("training6",revision,tool,rows.ToArray(),[new("unlock","Unlock All Regular Stages"),new("unlockDistribution","Unlock Regular & Distribution Stages")]);
    }
    void EditTraining6(string id,string mode,Dictionary<string,string> edits)
    {
        var training=((ISaveBlock6Main)RequireSave()).SuperTrain;
        if(mode is "unlock" or "unlockDistribution"){training.UnlockAllStages(mode=="unlockDistribution");return;}
        if(mode=="clearBags"){for(int i=0;i<12;i++)training.SetBag(i,0);return;}
        var parts=id.Split(':');int index=int.Parse(parts[1]);
        if(parts[0]=="bag") {
            if(edits.TryGetValue("Bag",out var bag))training.SetBag(index,byte.Parse(bag));
            int next=0;for(int i=0;i<12;i++){byte value=training.GetBag(i);if(value!=0)training.SetBag(next++,value);}for(int i=next;i<12;i++)training.SetBag(i,0);return;
        }
        if(parts[0]=="distribution"){if(edits.TryGetValue("Unlocked",out var flag))training.SetIsDistributionUnlocked(index,bool.Parse(flag));return;}
        if(mode=="clearRecords"){training.ClearRecord1(index);training.ClearRecord2(index);return;}
        foreach(var (key,value) in edits) {
            if(key=="Unlocked"){training.SetIsRegimenUnlocked(index,bool.Parse(value));continue;}
            int rank=key[^1]-'0';var holder=rank==1?training.GetHolder1(index):training.GetHolder2(index);
            switch(key[..^1]) {
                case "Time":float time=float.Parse(value,CultureInfo.InvariantCulture);if(rank==1)training.SetTime1(index,time);else training.SetTime2(index,time);break;
                case "Species":holder.Species=ushort.Parse(value);break;
                case "Form":holder.Form=byte.Parse(value);break;
                case "Gender":holder.Gender=byte.Parse(value);break;
            }
        }
    }
}
