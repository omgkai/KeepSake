import SwiftUI

struct MoveCheck: Codable {
    let slot: Int, relearn: Bool, move: Int, type: Int
    let status: String, detail: String, typeName: String
}
struct MoveStatusView: View {
    @EnvironmentObject var model: EditorModel
    let slot: Int
    var relearn = false
    private var check: MoveCheck? { model.state.moveChecks.first { $0.slot == slot && $0.relearn == relearn } }
    private var status: String { model.fieldDrafts ? "pending" : check?.status ?? "unknown" }
    private var color: Color { status == "legal" ? .green : status == "illegal" ? .red : .secondary }
    private var label: String { switch status { case "legal": "Legal"; case "illegal": "Illegal"; case "empty": "Empty"; case "pending": "Edits pending"; default: "Not evaluated" } }
    private var icon: String { status == "legal" ? "checkmark.seal.fill" : status == "illegal" ? "exclamationmark.triangle.fill" : "minus.circle" }
    var body: some View {
        HStack(alignment:.top,spacing:8) {
            Label(label,systemImage:icon).font(.caption.weight(.semibold)).fixedSize()
            Text(model.fieldDrafts ? "Press Return to check your edits." : check?.detail ?? "Select a Pokémon to check its moves.")
                .font(.caption).foregroundStyle(Color.primary.opacity(0.8)).frame(maxWidth:.infinity,alignment:.leading)
        }.foregroundStyle(color).padding(.horizontal,10).padding(.vertical,7)
            .background(color.opacity(0.09),in:RoundedRectangle(cornerRadius:7))
            .accessibilityElement(children:.combine)
    }
}
struct SuggestMovesButton: View {
    @EnvironmentObject var model: EditorModel
    var relearn = false
    var body: some View {
        HStack {
            Button { Task {await model.suggestMoves(mode:relearn ? "relearn" : "current")} } label: {
                Label(relearn ? "Suggest Relearn Moves" : "Suggest Moves",systemImage:"wand.and.stars")
            }.buttonStyle(.borderedProminent)
            if !relearn {
                Menu {
                    Button("Default PKHeX Set") {Task {await model.suggestMoves(mode:"level")}}
                    Button("Different Set") {Task {await model.suggestMoves(mode:"different")}}
                } label: {Image(systemName:"chevron.down")}.menuStyle(.borderlessButton).menuIndicator(.hidden).fixedSize().accessibilityLabel("Move suggestion options")
                    .help("Choose the default moves or try a different valid set")
            }
        }.disabled(model.busy || model.drafts.keys.contains{!$0.hasPrefix("entity|")} || model.state.entityName == "Empty slot")
            .help("Apply typed Pokémon edits and suggest moves. If the set already matches, try another. Undo restores both.")
    }
}

extension EditorModel {
    var pokemonDraftEdits:[[String:String]] {
        let values=drafts.filter{$0.key.hasPrefix("entity|")}
        let priority=["entity|Species","entity|Form","entity|Nickname"]
        return values.keys.sorted{(priority.firstIndex(of:$0) ?? 100,$0)<(priority.firstIndex(of:$1) ?? 100,$1)}
            .map{["field":String($0.dropFirst(7)),"value":values[$0]!]}
    }
    func suggestMoves(mode:String) async {
        guard !busy else{return};busy=true;defer{busy=false}
        let editedKeys=drafts.keys.filter{$0.hasPrefix("entity|")}
        do {
            state=try await bridge.send(["op":"suggestMoves","mode":mode,"edits":pokemonDraftEdits],as:EditorState.self)
            for key in editedKeys {drafts.removeValue(forKey:key)}
            status=state.suggestionMessage
            if moveRecords != nil,state.canMoveRecords {moveRecords=try await bridge.send(["op":"moveRecords"],as:MoveRecordData.self)}
        } catch {self.error=error.localizedDescription}
    }
    func fetchMoveChoices() async throws -> MoveChoiceData {
        guard !busy else{throw BridgeError(message:"Please wait for the current edit to finish.")}
        busy=true;defer{busy=false}
        return try await bridge.send(["op":"moveChoices","edits":pokemonDraftEdits],as:MoveChoiceData.self)
    }
}
