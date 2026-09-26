import SwiftUI

struct SampleWorkspacePicker:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var games:[Choice]=[]
    @State private var selected="PLA"
    @State private var search=""
    @State private var failure=""
    var body:some View {
        VStack(alignment:.leading,spacing:18){
            HStack{Label("Choose your sample adventure",systemImage:"book.pages.fill").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Explore that game’s Pokémon editor, trainer card and available tools. Samples are practice workspaces, not playable saves.").foregroundStyle(.secondary)
            TextField("Find a game…",text:$search).textFieldStyle(.roundedBorder)
            if !failure.isEmpty {Text(failure).foregroundStyle(.orange);Button("Try Again"){Task{await load()}}}
            ScrollView{LazyVGrid(columns:[GridItem(.adaptive(minimum:180))],spacing:12){ForEach(games.filter{search.isEmpty || $0.label.localizedCaseInsensitiveContains(search)}){game in
                Button{selected=game.value}label:{VStack(spacing:8){SaveGameLogo(version:game.value,name:game.label).frame(height:46);Text(game.label).font(.callout.weight(.semibold));Image(systemName:selected==game.value ? "checkmark.circle.fill":"circle").foregroundStyle(selected==game.value ? Color.accentColor:.secondary)}.padding(12).frame(maxWidth:.infinity,minHeight:114).background(selected==game.value ? Color.accentColor.opacity(0.12):Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:16)).overlay(RoundedRectangle(cornerRadius:16).stroke(selected==game.value ? Color.accentColor:Color.primary.opacity(0.08),lineWidth:1))}.buttonStyle(.plain).accessibilityLabel(game.label).accessibilityValue(selected==game.value ? "Selected":"Not selected")
            }}}
            HStack{Text(games.first{$0.value==selected}?.label ?? "Choose a game").font(.headline);Spacer();Button("Explore Sample"){model.startSample(selected)}.buttonStyle(.borderedProminent).disabled(!games.contains{$0.value==selected}||model.busy).keyboardShortcut(.defaultAction)}
        }.padding(24).frame(width:680,height:600).task{await load()}
    }
    private func load()async{do{games=try await model.bridge.send(["op":"sampleGames"],as:[Choice].self);failure=""}catch{failure=error.localizedDescription}}
}
