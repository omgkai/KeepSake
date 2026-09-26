using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

sealed partial class EditorSession {
    ExtraPage ReadWorld(ExtraTool tool) {
        var rows=new List<ExtraRow>();
        bool gen4=save is SAV4;
        IGeonet geo=gen4?new Geonet4((SAV4)save!.Clone()):((SAV5)save!).UnityTower;
        var global=new List<ExtraValue>{EV("GlobalFlag","Show world map",geo.GlobalFlag)};
        if(geo is UnityTower5 tower)global.Add(EP(tower,"UnityTowerFlag","Unlock Unity Tower"));
        rows.Add(ER("settings","World map",global.ToArray(),"The trainer's home location stays marked red."));
        var countries=Util.GetCountryRegionList(gen4?"gen4_countries":"gen5_countries","en");
        int max=gen4?LocaleNDS4.CountryCount:LocaleNDS5.CountryCount;
        Choice[] points=[new("0","Not visited"),new("1","Blue"),new("2","Yellow · visited"),new("3","Red · home")];
        for(int c=1;c<=max;c++) {
            byte country=(byte)c;string countryName=countries.FirstOrDefault(x=>x.Value==c)?.Text??$"Country {c}";
            if(geo is UnityTower5 ut)rows.Add(ER($"floor:{c}",countryName+" · Tower floor",[EV("Unlocked","Unlocked",ut.GetUnityTowerFloor(country))],"Unity Tower"));
            int count=gen4?Geonet4.GetSubregionCount(country):UnityTower5.GetSubregionCount(country);
            var regions=Util.GetCountryRegionList($"gen{(gen4?4:5)}_sr_"+(count==0?"default":$"{c:000}"),"en");
            for(int region=count==0?0:1;region<=count;region++) {
                string sub=regions.FirstOrDefault(x=>x.Value==region)?.Text??$"Region {region}";
                rows.Add(ER($"place:{c}:{region}",countryName+(count==0?"":" · "+sub),[EV("Point","Map marker",(int)geo.GetCountrySubregion(country,(byte)region),choices:points)],"Globe location"));
            }
        }
        return new(tool.id,revision,tool,rows.ToArray(),[new("legal","Mark All Legal Locations"),new("give","Mark All Locations"),new("clear","Clear Locations")]);
    }
    void EditWorld(string id,string mode,Dictionary<string,string> edits) {
        IGeonet geo=save is SAV4 s?new Geonet4(s):((SAV5)save!).UnityTower;
        if(mode=="legal")geo.SetAllLegal();else if(mode=="give")geo.SetAll();else if(mode=="clear")geo.ClearAll();
        else if(id=="settings") {if(edits.TryGetValue("GlobalFlag",out var global))geo.GlobalFlag=bool.Parse(global);if(edits.TryGetValue("UnityTowerFlag",out var tower))((UnityTower5)geo).UnityTowerFlag=bool.Parse(tower);}
        else {var parts=id.Split(':');byte country=byte.Parse(parts[1]);if(parts[0]=="floor")((UnityTower5)geo).SetUnityTowerFloor(country,bool.Parse(edits["Unlocked"]));else geo.SetCountrySubregion(country,byte.Parse(parts[2]),(GeonetPoint)int.Parse(edits["Point"]));}
        geo.SetSAVCountry();if(geo is Geonet4 g)g.Save();
    }
    ExtraPage ReadBerryPlots(ExtraTool tool) {
        var field=((SAV6XY)save!).BerryField;var rows=new List<ExtraRow>();
        for(int i=0;i<BerryField6XY.Count;i++) {var data=field.GetPlot(i);int berry=ReadUInt16LittleEndian(data);var values=new List<ExtraValue>();
            for(int j=0;j<8;j++)values.Add(new(j.ToString(),j==0?"Berry index":$"Stored value {j}",ReadUInt16LittleEndian(data[(j*2)..]).ToString(),"readonly","","",[]));
            rows.Add(ER(i.ToString(),$"Plot {i+1}",values.ToArray(),"Read-only inspection, matching the Windows Berry Field viewer. Undocumented values are preserved."));
        }
        return new(tool.id,revision,tool,rows.ToArray(),[]);
    }
}
