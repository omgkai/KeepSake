using System.Text.Json;
using PKHeX.Core;
sealed partial class EditorSession {
    static ISaveHandler[] ManualSaveHandlers()=>[new SaveHandlerDefault(),..SaveUtil.Handlers];
    object SaveOpenOptions()=>new {
        handlers=ManualSaveHandlers().Select((h,i)=>new Choice(i.ToString(),h.GetType().Name.Replace("SaveHandler",""))).ToArray(),
        types=Enum.GetValues<SaveFileType>().Where(x=>x!=SaveFileType.None).Select(x=>new Choice(x.ToString(),Label(x.ToString()))).ToArray(),
        versions=GameUtil.GameVersions.Distinct().Select(x=>new {value=x.ToString(),label=GameInfo.GetVersionName(x),type=x.SaveFileType.ToString()}).ToArray(),
        languages=Enum.GetValues<LanguageID>().Select(x=>new Choice(x.ToString(),Label(x.ToString()))).ToArray()
    };
    static SaveFile OpenWithHandler(byte[] data,string path,JsonElement r) {
        var handlers=ManualSaveHandlers();int id=N(r,"handler");
        if(id<0||id>=handlers.Length||!Enum.TryParse<SaveFileType>(S(r,"saveType"),out var type)||type==SaveFileType.None||!Enum.IsDefined(type))throw new Exception("Choose a supported save type and handler.");
        if(!Enum.TryParse<GameVersion>(S(r,"saveVersion","Any"),out var version)||!Enum.IsDefined(version)||(version!=GameVersion.Any&&version.SaveFileType!=type))throw new Exception("Choose an edition matching the save type.");
        if(!Enum.TryParse<LanguageID>(S(r,"saveLanguage","None"),out var language)||!Enum.IsDefined(language))throw new Exception("Choose a supported language.");
        if(!SaveUtil.TryGetSaveFileHandler(data,out var sav,path,handlers[id],new SaveTypeInfo(type,version,language)))throw new Exception("This file could not be opened with those save settings. The current workspace is unchanged.");
        return sav;
    }
}
