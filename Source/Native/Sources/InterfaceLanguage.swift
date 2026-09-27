import SwiftUI

enum InterfaceLanguage {
    static let choices:[(String,String)]=[("system","System language"),("en","English"),("ja","日本語"),("fr","Français"),("it","Italiano"),("de","Deutsch"),("es","Español"),("es-419","Español (Latinoamérica)"),("ko","한국어"),("zh-Hans","简体中文"),("zh-Hant","繁體中文")]
    static var current:String { resolve(UserDefaults.standard.string(forKey:"interfaceLanguage") ?? "system") }
    static func resolve(_ selected:String)->String {
        if choices.contains(where:{$0.0==selected}),selected != "system"{return selected}
        for language in Locale.preferredLanguages {
            if language.hasPrefix("zh"){return language.contains("Hant") || language.contains("TW") || language.contains("HK") ? "zh-Hant":"zh-Hans"}
            if language.hasPrefix("es-419"){return "es-419"}
            if let base=language.split(separator:"-").first,choices.contains(where:{$0.0==String(base)}){return String(base)}
        }
        return "en"
    }
    static func text(_ key:String)->String {
        guard let path=Bundle.main.path(forResource:current,ofType:"lproj"),let bundle=Bundle(path:path) else{return key}
        return bundle.localizedString(forKey:key,value:key,table:nil)
    }
}
func L(_ key:String)->String{InterfaceLanguage.text(key)}
struct InterfaceLanguageView:View {
    @AppStorage("interfaceLanguage") private var language="system"
    var body:some View {
        VStack(alignment:.leading,spacing:10){
            Picker("Interface language",selection:$language){ForEach(InterfaceLanguage.choices,id:\.0){code,name in Text(code == "system" ? L(name):name).tag(code)}}
            Text("Common editor labels and navigation use PKHeX translations. Some specialized tools and guidance remain in English.").font(.caption).foregroundStyle(.secondary)
        }.padding(20)
    }
}
struct InterfaceLocale:ViewModifier {
    @AppStorage("interfaceLanguage") private var language="system"
    func body(content:Content)->some View {content.environment(\.locale,Locale(identifier:InterfaceLanguage.resolve(language)))}
}
