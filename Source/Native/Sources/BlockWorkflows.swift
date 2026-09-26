import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct BlockDifference:Codable,Identifiable {
    let id:String,name:String,status:String,before:String,after:String,changedBytes:Int,firstOffset:Int,importable:Bool
    var color:Color {status == "Added" ? .green : status == "Removed" ? .red : status.contains("Type") || status.contains("Size") ? .orange : .blue}
    var symbol:String {status == "Added" ? "plus.circle.fill" : status == "Removed" ? "minus.circle.fill" : "arrow.triangle.2.circlepath"}
}
struct BlockReviewData:Codable,Identifiable {
    let token:String,revision:Int,source:String,comparison:Bool,unchanged:Int,entries:[BlockDifference],issues:[String],canApply:Bool
    var id:String {token}
}
struct BlockReviewSheet:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @Environment(\.gameTheme) private var theme
    let data:BlockReviewData
    @State private var search=""
    @State private var changedOnly="All changes"
    private var entries:[BlockDifference] {data.entries.filter{(changedOnly == "All changes" || $0.status == changedOnly) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.id.localizedCaseInsensitiveContains(search))}}
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack(spacing:14) {
                Image(systemName:data.comparison ? "doc.on.doc.fill" : "square.and.arrow.down.on.square.fill").font(.title).foregroundStyle(theme.accent).frame(width:52,height:52).background(theme.accent.opacity(0.12),in:RoundedRectangle(cornerRadius:14))
                VStack(alignment:.leading,spacing:4) {Text(data.comparison ? "Save comparison" : "Review block import").font(.title2.bold());Text(data.source).font(.callout).foregroundStyle(.secondary).lineLimit(1)}
                Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)
            }
            HStack(spacing:10) {summary(data.entries.count == 1 ? "Change" : "Changes",String(data.entries.count),"square.stack.3d.up");summary("Unchanged",String(data.unchanged),"equal.circle");summary(data.issues.count == 1 ? "Issue" : "Issues",String(data.issues.count),"exclamationmark.circle")}
            Text(data.comparison ? "Current workspace → selected save. Comparison never changes either save." : "Only the listed changes will be applied, together as one Undo step. Export a copy when you’re ready.").font(.callout).foregroundStyle(.secondary)
            if !data.issues.isEmpty {
                DisclosureGroup("\(data.issues.count) issues to resolve before importing") {ScrollView{VStack(alignment:.leading,spacing:6){ForEach(Array(data.issues.enumerated()),id:\.offset){_,issue in Text(issue).font(.caption).textSelection(.enabled)}}.frame(maxWidth:.infinity,alignment:.leading)}.frame(maxHeight:100)}.padding(12).background(.orange.opacity(0.1),in:RoundedRectangle(cornerRadius:12))
            }
            HStack {TextField("Find a block by name or key…",text:$search).textFieldStyle(.roundedBorder);Picker("Changes",selection:$changedOnly){Text("All changes").tag("All changes");ForEach(Array(Set(data.entries.map(\.status))).sorted(),id:\.self){Text($0)}}.frame(width:180)}
            ScrollView {LazyVStack(spacing:10) {ForEach(entries){row in
                HStack(alignment:.top,spacing:12) {
                    Image(systemName:row.symbol).foregroundStyle(row.color).padding(.top,3)
                    VStack(alignment:.leading,spacing:7) {
                        HStack {Text(row.name).font(.callout.bold()).lineLimit(1);Spacer();Text(row.status).font(.caption.bold()).foregroundStyle(row.color)}
                        Text(row.id).font(.caption.monospaced()).foregroundStyle(.secondary)
                        HStack(alignment:.top,spacing:14) {fact("CURRENT",row.before);Image(systemName:"arrow.right").foregroundStyle(.tertiary);fact(data.comparison ? "OTHER SAVE" : "AFTER IMPORT",row.after)}
                        if row.firstOffset>=0 {Text("\(row.changedBytes.formatted()) changed \(row.changedBytes == 1 ? "byte" : "bytes") · first difference at 0x\(String(row.firstOffset,radix:16).uppercased())").font(.caption2).foregroundStyle(.secondary)}
                    }
                }.padding(14).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:14))
            }}}.overlay {if entries.isEmpty {ContentUnavailableView(data.entries.isEmpty ? "No changed blocks" : "No matching blocks",systemImage:"checkmark.circle",description:Text(data.entries.isEmpty ? (data.issues.isEmpty ? "The compared block values already match." : "No compatible changes were found. Review the issues above.") : "Try another name or key."))}}
            HStack {
                Button("Export Report…"){exportReport()}
                Spacer()
                if !data.comparison {Button("Apply \(data.entries.count) \(data.entries.count == 1 ? "Change" : "Changes")") {Task{await model.command(["op":"saveBlocksApply","token":data.token,"revision":data.revision],status:"Imported save blocks — export a copy to save");if model.state.revision != data.revision && model.error == nil {dismiss()}}}.buttonStyle(.borderedProminent).disabled(!data.canApply || model.busy || model.fieldDrafts || model.state.pending || model.state.revision != data.revision)}
            }
            if model.state.revision != data.revision {Label("The workspace changed. Close this review and preview again.",systemImage:"arrow.clockwise").font(.caption).foregroundStyle(.orange)}
        }.padding(24).frame(width:790,height:680)
    }
    private func summary(_ title:String,_ value:String,_ symbol:String)->some View {HStack{Image(systemName:symbol).foregroundStyle(theme.accent);Text(value).font(.title3.bold());Text(title).font(.caption).foregroundStyle(.secondary)}.padding(12).frame(maxWidth:.infinity).background(theme.accent.opacity(0.07),in:RoundedRectangle(cornerRadius:12))}
    private func fact(_ title:String,_ value:String)->some View {VStack(alignment:.leading,spacing:3){Text(title).font(.system(size:9,weight:.semibold)).tracking(1).foregroundStyle(.secondary);Text(value).font(.caption.monospaced()).textSelection(.enabled)}.frame(maxWidth:.infinity,alignment:.leading)}
    private func exportReport() {
        let panel=NSSavePanel();panel.nameFieldStringValue="KeepSake Block Report.txt";panel.allowedContentTypes=[.plainText]
        guard panel.runModal() == .OK,let url=panel.url else{return}
        let header="KeepSake \(data.comparison ? "comparison" : "import review") · \(data.source)\n\(data.entries.count) changes · \(data.unchanged) unchanged\n\n"
        let lines=data.entries.map{"\($0.id) · \($0.name) · \($0.status)\n  Current: \($0.before)\n  Other: \($0.after)\n  Changed bytes: \($0.changedBytes); first offset: \($0.firstOffset)\n"}
        do{try (header+lines.joined(separator:"\n")+"\n"+data.issues.joined(separator:"\n")).write(to:url,atomically:true,encoding:.utf8)}catch{model.error=error.localizedDescription}
    }
}
struct RawBlockExportSheet:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var mode="Serialized blocks"
    @State private var dataOnly=false
    @State private var key=true
    @State private var type=true
    @State private var header=false
    var body:some View {VStack(alignment:.leading,spacing:18) {
        HStack {Label("Raw block export",systemImage:"doc.badge.gearshape").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}}
        Text("Export the current block collection as one binary file, using PKHeX’s export formats.").foregroundStyle(.secondary)
        Picker("Format",selection:$mode){Text("Serialized blocks").tag("Serialized blocks");Text("Custom binary layout").tag("Custom binary layout")}.pickerStyle(.segmented)
        if mode == "Custom binary layout" {Toggle("Skip blocks without data",isOn:$dataOnly);Toggle("Include block keys",isOn:$key);Toggle("Include type information",isOn:$type);Toggle("Include BLOCK labels",isOn:$header)}
        Text(mode == "Serialized blocks" ? "Produces PKHeX’s decrypted raw block stream." : "Options match the Windows block-dump controls. KeepSake’s ZIP archive is the format for restoring typed flags.").font(.caption).foregroundStyle(.secondary)
        HStack{Spacer();Button("Export…"){export()}.buttonStyle(.borderedProminent)}
    }.padding(24).frame(width:550).disabled(model.busy)}
    private func export(){let panel=NSSavePanel();panel.nameFieldStringValue="raw.bin";guard panel.runModal() == .OK,let url=panel.url else{return};let options=mode == "Serialized blocks" ? 0:(dataOnly ? 1:0)|(key ? 2:0)|(type ? 4:0)|(header ? 8:0);Task{do{_ = try await model.bridge.send(["op":"saveBlocksRawExport","options":options,"path":url.path],as:PathResult.self);dismiss()}catch{model.error=error.localizedDescription}}}
}
