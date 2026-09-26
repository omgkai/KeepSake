import SwiftUI

struct TrainerDetailsView: View {
    @EnvironmentObject private var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var fields: [Field] = []
    @State private var page="Details"
    @State private var trainerNames=false
    @State private var nameSupported=false
    var body: some View {
        VStack(alignment:.leading,spacing:14) {
            HStack {
                Label("Adventure details",systemImage:"map.fill").font(.title2.bold())
                Spacer()
                if nameSupported { Button("Name Bytes…") {trainerNames=true}.disabled(model.fieldDrafts) }
                Button("Done") { dismiss() }.disabled(model.drafts.keys.contains{$0.hasPrefix("trainerDetail|")}).keyboardShortcut(.cancelAction)
            }.padding(.horizontal,24).padding(.top,24)
            Text("Game-specific progress, location and dates. Changes support Undo and are saved when you export your save.").font(.callout).foregroundStyle(.secondary).padding(.horizontal,24)
            Picker("Page",selection:$page) { Text("Details").tag("Details"); Text("Game Photos").tag("Photos") }.pickerStyle(.segmented).padding(.horizontal,24)
            if page == "Photos" { TrainerPhotosView() }
            else if fields.isEmpty { ContentUnavailableView("No additional fields",systemImage:"map",description:Text("This game’s available trainer fields are on the trainer card.")) }
            else { FieldList(fields:fields,target:"trainerDetail",grouped:true) }
            Text("Date and time: YYYY-MM-DD HH:mm:ss. Location values use the game’s coordinate system.").font(.caption).foregroundStyle(.secondary).padding(.horizontal,24).padding(.bottom,20)
        }.frame(width:760,height:640).sheet(isPresented:$trainerNames){NameBytesEditor(trainer:true)}.task(id:model.state.revision) {
            do { nameSupported=try await model.bridge.send(["op":"trainerNameSupported"],as:Bool.self); fields=try await model.bridge.send(["op":"trainerDetails"],as:[Field].self) } catch { model.error=error.localizedDescription }
        }
    }
}
