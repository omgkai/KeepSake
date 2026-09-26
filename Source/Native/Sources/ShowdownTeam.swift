import SwiftUI
struct ShowdownTeamSheet:View {
    @Environment(\.dismiss) private var dismiss
    @EnvironmentObject var model:EditorModel
    @State private var text=""
    @State private var preview=false
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            Label("Import a Showdown team",systemImage:"person.3.fill").font(.title2.bold())
            Text("Paste up to six Pokémon sets, separated by blank lines. Each is generated for your open game and checked for legality. Review the whole team before adding it to empty box slots or exporting files.").foregroundStyle(.secondary)
            TextEditor(text:$text).font(.body.monospaced()).border(.quaternary)
            HStack {Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction);Spacer();Button("Review Team"){preview=true}.buttonStyle(.borderedProminent).disabled(text.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty || model.busy || model.state.pending)}
        }.padding(24).frame(width:700,height:580).sheet(isPresented:$preview){GenerationSheet(team:nil,showdown:text)}
    }
}
