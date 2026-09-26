import SwiftUI

struct JournalMark:View {
    var body:some View {
        GeometryReader {g in
            ZStack {
                RoundedRectangle(cornerRadius:g.size.width*0.14).fill(LinearGradient(colors:[Color(hex:"152942"),Color(hex:"304B6B")],startPoint:.topLeading,endPoint:.bottomTrailing))
                RoundedRectangle(cornerRadius:g.size.width*0.1).stroke(Color(hex:"E3C576"),lineWidth:1.2).padding(g.size.width*0.09)
                Rectangle().fill(Color(hex:"E3C576").opacity(0.45)).frame(width:1).offset(x: -g.size.width*0.32)
                ZStack {
                    Circle().stroke(Color(hex:"E3C576"),lineWidth:2)
                    Rectangle().fill(Color(hex:"E3C576")).frame(height:2)
                    Circle().fill(Color(hex:"203852")).overlay(Circle().stroke(Color(hex:"E3C576"),lineWidth:2)).frame(width:g.size.width*0.14,height:g.size.width*0.14)
                }.frame(width:g.size.width*0.46,height:g.size.width*0.46).offset(x:g.size.width*0.035)
            }
        }.aspectRatio(0.82,contentMode:.fit).accessibilityLabel("KeepSake journal")
    }
}
struct PersonalColorPreset:Codable,Identifiable {
    var id=UUID().uuidString
    var name:String,first:String,second:String
}
struct ColorPresetsPicker:View {
    @Binding var first:String
    @Binding var second:String
    var onSelect:()->Void = {}
    @AppStorage("keepsakeColorPresets") private var saved=Data()
    @State private var name=""
    private var presets:[PersonalColorPreset] {(try? JSONDecoder().decode([PersonalColorPreset].self,from:saved)) ?? []}
    var body:some View {
        VStack(alignment:.leading,spacing:10) {
            HStack {
                TextField("Preset name",text:$name).textFieldStyle(.roundedBorder)
                Button("Save Colors") {
                    var next=presets
                    let title=String(name.trimmingCharacters(in:.whitespacesAndNewlines).prefix(50))
                    if let index=next.firstIndex(where:{$0.name==title}) {next[index].first=first;next[index].second=second}
                    else {next.append(PersonalColorPreset(name:title,first:first,second:second))}
                    write(next);name=""
                }.disabled(name.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty || (presets.count>=50 && !presets.contains{$0.name==name.trimmingCharacters(in:.whitespacesAndNewlines)}))
            }
            if !presets.isEmpty {ScrollView(.horizontal) {HStack(spacing:8) {ForEach(presets){preset in
                Button {first=preset.first;second=preset.second;onSelect()} label:{HStack(spacing:5){Circle().fill(Color(hex:preset.first)).frame(width:12,height:12);Circle().fill(Color(hex:preset.second)).frame(width:12,height:12);Text(preset.name)}}
                    .contextMenu{Button("Delete Preset",role:.destructive){write(presets.filter{$0.id != preset.id})}}
            }}}}
            Text("Color presets are shared by your journal covers and app appearance on this Mac.").font(.caption).foregroundStyle(.secondary)
        }
    }
    private func write(_ values:[PersonalColorPreset]) {if let data=try? JSONEncoder().encode(values){saved=data}}
}
struct JournalCustomColors:View {
    @Binding var first:String?
    @Binding var second:String?
    var style:String
    private var enabled:Binding<Bool> {Binding(get:{first != nil && second != nil},set:{value in first=value ? journalColors(style)[0].rgbHex:nil;second=value ? journalColors(style)[1].rgbHex:nil})}
    private func hex(_ value:Binding<String?>,_ fallback:String)->Binding<String> {Binding(get:{value.wrappedValue ?? fallback},set:{value.wrappedValue=$0})}
    var body:some View {
        VStack(alignment:.leading,spacing:12) {
            Toggle("Custom cover colors",isOn:enabled).toggleStyle(.switch)
            if enabled.wrappedValue {
                HStack {ColorPicker("Cover",selection:Binding(get:{Color(hex:first ?? "142640")},set:{first=$0.rgbHex}),supportsOpacity:false);ColorPicker("Companion",selection:Binding(get:{Color(hex:second ?? "304A68")},set:{second=$0.rgbHex}),supportsOpacity:false)}
                ColorPresetsPicker(first:hex($first,"142640"),second:hex($second,"304A68"))
            }
        }
    }
}
