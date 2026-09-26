using PKHeX.Core;
using static PKHeX.Core.SaveBlockAccessor9SV;

sealed partial class EditorSession
{
    ExtraPage ReadRegisteredTeams(ExtraTool tool)
    {
        var sav=(SAV_STADIUM)RequireSave();var groups=sav.GetRegisteredTeams();var rows=new List<ExtraRow>();
        for(int team=0;team<groups.Length;team++)for(int member=0;member<groups[team].Slots.Length;member++) {
            var pk=groups[team].Slots[member];rows.Add(ER($"{team}:{member}",$"{member+1} · {(pk.Species==0?"Empty":pk.IsNicknamed?pk.Nickname:Species(pk.Species))}",[],pk.Species==0?"Empty slot":$"{Species(pk.Species)} · Lv. {pk.CurrentLevel}",SpriteFor(pk.Species,pk.Form,0,0,pk.Context,pk.IsShiny),pk.Species==0?[]:[new("view","View Pokémon")]) with{category=$"{team+1} · {groups[team].GroupName}"});
        }
        return new(tool.id,revision,tool,rows.ToArray(),[]);
    }
    ExtraPage ReadTrainerTeams(ExtraTool tool)
    {
        var s=(SAV8SWSH)RequireSave();var rows=new List<ExtraRow>();
        foreach(var kind in new[]{"card","title"})for(int i=0;i<6;i++) {
            object member=kind=="card"?s.TrainerCard.ViewPoke(i):s.TitleScreen.ViewPoke(i);
            var fields=Fields(member,"Team").Where(f=>f.editable).Select(f=>new ExtraValue(f.id,f.label,f.value,f.kind,"0",f.id=="Species"?s.MaxSpeciesID.ToString():f.id=="Gender"?"2":f.id=="Form"?"255":f.id=="FormArgument"?int.MaxValue.ToString():uint.MaxValue.ToString(),f.choices)).ToArray();
            rows.Add(ER($"{kind}:{i}",$"{(kind=="card"?"League card":"Title screen")} · Pokémon {i+1}",fields,Species(((ISpeciesForm)member).Species),actions:[new("clear","Clear Pokémon")]) with {category=kind=="card"?"League card":"Title screen"});
        }
        return new(tool.id,revision,tool,rows.ToArray(),[new("cardParty","Copy Party to League Card"),new("titleParty","Copy Party to Title Screen")]);
    }
    void EditTrainerTeams(string id,string mode,Dictionary<string,string> edits)
    {
        var s=(SAV8SWSH)RequireSave();if(mode=="cardParty"){s.TrainerCard.SetPartyData();return;}if(mode=="titleParty"){s.TitleScreen.SetPartyData();return;}
        var parts=id.Split(':');int index=int.Parse(parts[1]);object member=parts[0]=="card"?s.TrainerCard.ViewPoke(index):s.TitleScreen.ViewPoke(index);
        if(mode=="clear"){if(member is TrainerCard8Poke card)card.Clear();else ((TitleScreen8Poke)member).Clear();return;}
        SetExtraProperties(member,edits);
    }
    ExtraPage ReadTrainerUnlocks(ExtraTool tool)
    {
        var actions=save switch {
            SAV7b=>new[]{new Choice("titles","Unlock All Trainer Titles")},
            SAV8SWSH=>new[]{new Choice("diglett","Find All Diglett"),new Choice("appearance","Reset Trainer Appearance")},
            SAV9SV=>new[]{new Choice("fly","Unlock Fly Destinations"),new Choice("stakes","Collect All Stakes"),new Choice("recipes","Unlock TM Recipes"),new Choice("snacks","Activate Snacksworth Legendaries"),new Choice("coaches","Unlock Coaches"),new Choice("ride","Unlock Ride Upgrades"),new Choice("throws","Unlock Throw Styles")},
            SAV9ZA=>new[]{new Choice("machines","Collect Technical Machines"),new Choice("screws","Collect Colorful Screws")},
            _=>Array.Empty<Choice>()};
        return new(tool.id,revision,tool,actions.Select(a=>ER(a.value,a.label,[],"Changes the corresponding in-game progress. Undo restores the previous state.",actions:[new(a.value,a.label)])).ToArray(),[]);
    }
    void EditTrainerUnlocks(string mode)
    {
        if(save is SAV7b lg){lg.EventWork.UnlockAllTitleFlags();return;}
        if(save is SAV8SWSH sw){if(mode=="diglett")sw.UnlockAllDiglett();else if(mode=="appearance")sw.MyStatus.ResetAppearance((PlayerSkinColor8)((Math.Max(0,(int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(sw.MyStatus.Skin)) & ~1) | sw.Gender));return;}
        if(save is SAV9ZA za){if(mode=="machines")TechnicalMachine9a.SetAllTechnicalMachines(za,true);else if(mode=="screws")ColorfulScrew9a.SetAllScrews(za);return;}
        var sv=(SAV9SV)RequireSave();switch(mode){
            case "fly":foreach(var key in TrainerFlyKeys)if(sv.Accessor.TryGetBlock(key,out var block))block.ChangeBooleanType(SCTypeCode.Bool2);break;
            case "stakes":sv.CollectAllStakes();break;case "recipes":sv.UnlockAllTMRecipes();break;
            case "snacks":sv.ActivateSnacksworthLegendaries();break;case "coaches":sv.UnlockAllCoaches();break;case "throws":sv.UnlockAllThrowStyles();break;
            case "ride":foreach(var name in new[]{"FSYS_RIDE_DASH_ENABLE","FSYS_RIDE_SWIM_ENABLE","FSYS_RIDE_HIJUMP_ENABLE","FSYS_RIDE_GLIDE_ENABLE","FSYS_RIDE_CLIMB_ENABLE","FSYS_RIDE_FLIGHT_ENABLE"})if(sv.Accessor.TryGetBlock(name,out var ride))ride.ChangeBooleanType(SCTypeCode.Bool2);break;
        }
    }
    // Fly destinations from the pinned upstream Windows trainer editor.
    static ReadOnlySpan<uint> TrainerFlyKeys =>
    [
        #region Fly Flags
        FSYS_YMAP_FLY_01,
        FSYS_YMAP_FLY_02,
        FSYS_YMAP_FLY_03,
        FSYS_YMAP_FLY_04,
        FSYS_YMAP_FLY_05,
        FSYS_YMAP_FLY_06,
        FSYS_YMAP_FLY_07,
        FSYS_YMAP_FLY_08,
        FSYS_YMAP_FLY_09,
        FSYS_YMAP_FLY_10,
        FSYS_YMAP_FLY_11,
        FSYS_YMAP_FLY_12,
        FSYS_YMAP_FLY_13,
        FSYS_YMAP_FLY_14,
        FSYS_YMAP_FLY_15,
        FSYS_YMAP_FLY_16,
        FSYS_YMAP_FLY_17,
        FSYS_YMAP_FLY_18,
        FSYS_YMAP_FLY_19,
        FSYS_YMAP_FLY_20,
        FSYS_YMAP_FLY_21,
        FSYS_YMAP_FLY_22,
        FSYS_YMAP_FLY_23,
        FSYS_YMAP_FLY_24,
        FSYS_YMAP_FLY_25,
        FSYS_YMAP_FLY_26,
        FSYS_YMAP_FLY_27,
        FSYS_YMAP_FLY_28,
        FSYS_YMAP_FLY_29,
        FSYS_YMAP_FLY_30,
        FSYS_YMAP_FLY_31,
        FSYS_YMAP_FLY_32,
        FSYS_YMAP_FLY_33,
        FSYS_YMAP_FLY_34,
        FSYS_YMAP_FLY_35,
        FSYS_YMAP_FLY_MAGATAMA,
        FSYS_YMAP_FLY_MOKKAN,
        FSYS_YMAP_FLY_TSURUGI,
        FSYS_YMAP_FLY_UTSUWA,
        FSYS_YMAP_POKECEN_02,
        FSYS_YMAP_POKECEN_03,
        FSYS_YMAP_POKECEN_04,
        FSYS_YMAP_POKECEN_05,
        FSYS_YMAP_POKECEN_06,
        FSYS_YMAP_POKECEN_07,
        FSYS_YMAP_POKECEN_08,
        FSYS_YMAP_POKECEN_09,
        FSYS_YMAP_POKECEN_10,
        FSYS_YMAP_POKECEN_11,
        FSYS_YMAP_POKECEN_12,
        FSYS_YMAP_POKECEN_13,
        FSYS_YMAP_POKECEN_14,
        FSYS_YMAP_POKECEN_15,
        FSYS_YMAP_POKECEN_16,
        FSYS_YMAP_POKECEN_17,
        FSYS_YMAP_POKECEN_18,
        FSYS_YMAP_POKECEN_19,
        FSYS_YMAP_POKECEN_20,
        FSYS_YMAP_POKECEN_21,
        FSYS_YMAP_POKECEN_22,
        FSYS_YMAP_POKECEN_23,
        FSYS_YMAP_POKECEN_24,
        FSYS_YMAP_POKECEN_25,
        FSYS_YMAP_POKECEN_26,
        FSYS_YMAP_POKECEN_27,
        FSYS_YMAP_POKECEN_28,
        FSYS_YMAP_POKECEN_29,
        FSYS_YMAP_POKECEN_30,
        FSYS_YMAP_POKECEN_31,
        FSYS_YMAP_POKECEN_32,
        FSYS_YMAP_POKECEN_33,
        FSYS_YMAP_POKECEN_34,
        FSYS_YMAP_POKECEN_35,

        // Treasures of Ruin shrine toggles
        FSYS_YMAP_MAGATAMA,
        FSYS_YMAP_MOKKAN,
        FSYS_YMAP_TSURUGI,
        FSYS_YMAP_UTSUWA,

        // Sudachi 1
        FSYS_YMAP_SU1MAP_CHANGE, // can change map to Kitakami
        FSYS_YMAP_FLY_SU1_AREA10,
        FSYS_YMAP_FLY_SU1_BUSSTOP,
        FSYS_YMAP_FLY_SU1_CENTER01,
        FSYS_YMAP_FLY_SU1_PLAZA,
        FSYS_YMAP_FLY_SU1_SPOT01,
        FSYS_YMAP_FLY_SU1_SPOT02,
        FSYS_YMAP_FLY_SU1_SPOT03,
        FSYS_YMAP_FLY_SU1_SPOT04,
        FSYS_YMAP_FLY_SU1_SPOT05,
        FSYS_YMAP_FLY_SU1_SPOT06,

        // Sudachi 2
        FSYS_YMAP_S2_MAPCHANGE_ENABLE, // can change map to Blueberry Academy
        FSYS_YMAP_FLY_SU2_DRAGON,
        FSYS_YMAP_FLY_SU2_ENTRANCE,
        FSYS_YMAP_FLY_SU2_FAIRY,
        FSYS_YMAP_FLY_SU2_HAGANE,
        FSYS_YMAP_FLY_SU2_HONOO,
        FSYS_YMAP_FLY_SU2_SPOT01,
        FSYS_YMAP_FLY_SU2_SPOT02,
        FSYS_YMAP_FLY_SU2_SPOT03,
        FSYS_YMAP_FLY_SU2_SPOT04,
        FSYS_YMAP_FLY_SU2_SPOT05,
        FSYS_YMAP_FLY_SU2_SPOT06,
        FSYS_YMAP_FLY_SU2_SPOT07,
        FSYS_YMAP_FLY_SU2_SPOT08,
        FSYS_YMAP_FLY_SU2_SPOT09,
        FSYS_YMAP_FLY_SU2_SPOT10,
        FSYS_YMAP_FLY_SU2_SPOT11,
        FSYS_YMAP_POKECEN_SU02,
        #endregion
    ];
}
