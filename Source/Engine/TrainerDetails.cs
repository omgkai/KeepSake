using System.Globalization;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    static readonly string[] TrainerDetailRoots = ["PokeFinder", "SUBE", "FieldMenu", "PlayerGeoLocation.AdventureBegin", "BattleTower", "Situation", "Coordinates", "LastSaved", "EnrollmentDate", "AdventureStart", "StartTime", "Played", "Festa", "FieldMoveModelSave", "System", "Player", "BlueberryQuestRecord", "BlueberryClubRoom", "PlayerAppearance", "InfiniteRoyale", "MyStatus", "Status", "Misc", "Config", "TrainerCard"];
    Field[] TrainerDetailFields()
    {
        var sav = RequireSave();
        var result = new List<Field>();
        string[] extra = ["Country", "Region", "ConsoleRegion", "GameSyncID", "X", "Y", "Z", "M", "R", "BP", "Coins", "Watts", "Badges", "Badges16", "SecondsToStart", "SecondsToFame", "BlueberryPoints", "ThrowStyle", "RoyalePoints", "LeagueCardNumber", "TrainerCardNumber", "Skin", "SkinColor", "RivalName", "Coin", "PikaFriendship", "PikaBeachScore", "BattleEffects", "BattleStyleSwitch", "Sound", "TextSpeed", "TextBoxFrame", "BattleScene", "BattleStyle", "Vivillon", "Map", "MapID", "ZoneID", "MultiplayerSpriteID", "BirthMonth", "BirthDay", "CurrentOT", "SelfIntroduction", "PlayerID"];
        result.AddRange(SaveFields(sav).Where(f => (extra.Contains(f.id) || sav is SAV4BR && (f.id.StartsWith("Record") || f.id.StartsWith("Unlocked"))) && f.editable).Select(f => f with { group = f.id is "X" or "Y" or "Z" or "M" or "R" ? "Location" : "Adventure" }));
        foreach (var name in TrainerDetailRoots)
        {
            object? child; try { child = Resolve(sav, name); } catch { continue; }
            if (child is null || child.GetType().IsByRefLike || child.GetType().IsValueType) continue;
            var group = name is "Coordinates" or "Situation" ? "Location" : name == "Played" ? "Last saved" : Label(name);
            var fields = Fields(child, group, name + ".").Where(f => f.editable && !f.id.EndsWith(".PlayedHours") && !f.id.EndsWith(".PlayedMinutes") && !f.id.EndsWith(".PlayedSeconds") && !new[]{"OT","ID32","TID16","SID16","Gender","Language","Game","Version","CurrentBox","BoxCount","PartyCount"}.Contains(f.id.Split('.').Last()));
            // Keep one authoritative representation for dates instead of also showing epoch fragments.
            if (fields.Any(f => f.kind == "datetime")) fields = fields.Where(f => f.kind == "datetime" && !f.id.Contains(".LocalTimestamp"));
            result.AddRange(fields);
        }
        if(sav.Generation>=3 && Property(sav,"Language")?.SetMethod?.IsPublic == true)
            result.Add(new("Language","Game language","Adventure",sav.Language.ToString(),"enum",true,"Changing the save language does not translate existing names.",null,GameInfo.LanguageDataSource(sav.Generation,sav.Context).Select(x=>new Choice(x.Value.ToString(),x.Text)).ToArray()));
        var editions=TrainerEditions(sav);if(editions.Length>0)result.Add(new("GameEdition","Game edition","Adventure",sav.Version.ToString(),"enum",true,"Changes the saved edition within this compatible game pair.",null,editions.Select(v=>new Choice(v.ToString(),GameInfo.GetVersionName(v))).ToArray()));
        if(sav is SAV4 or SAV5 or SAV6 or SAV7)foreach(var (id,label,value) in new[]{("AdventureDate","Adventure started",sav.SecondsToStart),("FameDate","First Hall of Fame",sav.SecondsToFame)})
            result.Add(new(id,label,"Dates",new DateTime(2000,1,1).AddSeconds(value).ToString("yyyy-MM-dd HH:mm:ss"),"datetime",true,"Stored in the game's local time.",null,[]));
        if(sav is SAV8SWSH skin)result.Add(new("SkinPalette","Skin tone","Appearance",((int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(skin.MyStatus.Skin)).ToString(),"enum",true,"Updates the matching trainer and parent skin values.",null,ExtraEnums<PlayerSkinColor8>()));
        object? rotation=TrainerRotationTarget(sav);
        if(rotation!=null) {
            double z=Convert.ToDouble(Property(rotation,sav is SAV9SV or SAV9ZA ? "RY" : "RZ")!.GetValue(rotation)),w=Convert.ToDouble(Property(rotation,"RW")!.GetValue(rotation));
            result.RemoveAll(f=>new[]{"RX","RY","RZ","RW"}.Contains(f.id.Split('.').Last()));
            result.Add(new("FacingDegrees","Facing direction (degrees)","Location",(Math.Atan2(z,w)*360/Math.PI).ToString(CultureInfo.InvariantCulture),"number",true,"-360 to 360 degrees.",null,[]));
        }
        return result.ToArray();
    }
    static GameVersion[] TrainerEditions(SaveFile sav)=>sav switch {SAV6XY=>[GameVersion.X,GameVersion.Y],SAV6AO=>[GameVersion.OR,GameVersion.AS],SAV7SM=>[GameVersion.SN,GameVersion.MN],SAV7USUM=>[GameVersion.US,GameVersion.UM],SAV7b=>[GameVersion.GP,GameVersion.GE],SAV8SWSH=>[GameVersion.SW,GameVersion.SH],SAV8BS=>[GameVersion.BD,GameVersion.SP],SAV9SV=>[GameVersion.SL,GameVersion.VL],_=>[]};
    static object? TrainerRotationTarget(SaveFile sav)=>sav switch {SAV7b s=>s.Coordinates,SAV8SWSH s=>s.Coordinates,SAV8LA s=>s.Coordinates,SAV9SV s=>s,SAV9ZA s=>s.Coordinates,_=>null};
    void SetTrainerDetail(JsonElement r)
    {
        var sav = RequireSave();
        var id = S(r, "field");
        var field = TrainerDetailFields().FirstOrDefault(f => f.id == id) ?? throw new Exception("This trainer field is unavailable for the loaded game.");
        var value = S(r, "value");
        if (field.kind == "number" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && !double.IsFinite(number)) throw new Exception("Enter a finite number.");
        if(id=="GameEdition") {if(!Enum.TryParse<GameVersion>(value,out var edition)||!TrainerEditions(sav).Contains(edition))throw new Exception("Choose a compatible game edition.");SetProperty(sav,"Version",edition.ToString());}
        else if(id=="SkinPalette" && sav is SAV8SWSH skin) {if(!int.TryParse(value,out int n)||!Enum.IsDefined(typeof(PlayerSkinColor8),n))throw new Exception("Choose a skin tone from the list.");skin.MyStatus.SetSkinColor((PlayerSkinColor8)n);}
        else if(id is "AdventureDate" or "FameDate") {
            var date=DateTime.ParseExact(value,"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);double seconds=(date-new DateTime(2000,1,1)).TotalSeconds;
            if(seconds<0 || seconds>uint.MaxValue)throw new Exception("Choose a date within this game's supported range (2000–2136).");
            if(id=="AdventureDate")sav.SecondsToStart=(uint)seconds;else sav.SecondsToFame=(uint)seconds;
        }
        else if(id=="FacingDegrees") {
            double degrees=double.Parse(value,CultureInfo.InvariantCulture);if(!double.IsFinite(degrees)||Math.Abs(degrees)>360)throw new Exception("Choose a direction from -360 to 360 degrees.");
            if(sav is SAV9ZA za)za.Coordinates.SetPlayerRotation(degrees);
            else if(sav is SAV9SV sv){double a=degrees*Math.PI/360;sv.SetPlayerRotation(0,(float)Math.Sin(a),0,(float)Math.Cos(a));}
            else {var target=TrainerRotationTarget(sav)!;double angle=degrees*Math.PI/360;foreach(var (key,v) in new[]{("RX",0f),("RY",0f),("RZ",(float)Math.Sin(angle)),("RW",(float)Math.Cos(angle))})Property(target,key)!.SetValue(target,v);}
        }
        else if (id == "MyStatus.Watt" && sav is SAV8SWSH watts && uint.TryParse(value,out var watt)) {
            if(watt>9999999)throw new Exception("Watts must be between 0 and 9,999,999.");
            watts.MyStatus.Watt=watt;
            if(watts.GetRecord(Record8.WattTotal)<watt)watts.SetRecord(Record8.WattTotal,(int)watt);
        }
        else if(id is "MyStatus.Number" or "TrainerCard.Number" && sav is SAV8SWSH card) { if(value.Length>3 || !value.All(char.IsAsciiDigit))throw new Exception("Use up to three digits for the league number.");card.MyStatus.Number=card.Blocks.TrainerCard.Number=value; }
        else SetProperty(sav, id, value);
        dirty = true;ParseSettings.InitFromSaveFileData(sav);
    }
}
