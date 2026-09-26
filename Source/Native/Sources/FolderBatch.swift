import SwiftUI

struct FolderBatchView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let token:String,ids:[Int]
    @State private var text="=Species=25\n.CurrentLevel=50"
    @State private var preview:BatchPreview?
    @State private var working=false
    @State private var exported:String?
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Label("Batch edit library files",systemImage:"square.stack.3d.up").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Preview changes to \(ids.count) visible library files. Edited copies are exported to a new folder. Originals and the open save stay unchanged.").foregroundStyle(.secondary)
            TextEditor(text:$text).font(.body.monospaced()).frame(height:130).padding(8).background(.quaternary,in:RoundedRectangle(cornerRadius:10)).onChange(of:text){_,_ in preview=nil;exported=nil}
            HStack {Text("= filters · . edits · ; separates sets").font(.caption).foregroundStyle(.secondary);Spacer();Button("Preview Changes"){runPreview()}.buttonStyle(.borderedProminent)}
            if let preview {
                HStack {Text("\(preview.count) changed files").font(.headline);Spacer();Button("Export Edited Copies…"){export(preview)}.disabled(preview.count==0 || !preview.errors.isEmpty || exported != nil)}
                ScrollView {LazyVStack(alignment:.leading,spacing:12){
                    ForEach(preview.errors,id:\.self){Text($0).foregroundStyle(.red)}
                    ForEach(Array(preview.changes.enumerated()),id:\.offset){_,change in VStack(alignment:.leading,spacing:4){Text(change.location).fontWeight(.semibold);Text(change.detail).font(.caption).foregroundStyle(.secondary)}.frame(maxWidth:.infinity,alignment:.leading)}
                }}
            } else {ContentUnavailableView("Review before exporting",systemImage:"doc.text.magnifyingglass",description:Text("Use the same filter and edit syntax as the save batch editor."))}
            if let exported{Label("Exported to \(exported)",systemImage:"checkmark.circle.fill").font(.caption).foregroundStyle(.green).textSelection(.enabled)}
        }.padding(24).frame(width:760,height:640).disabled(working).interactiveDismissDisabled(working)
    }
    private func runPreview(){working=true;preview=nil;exported=nil;Task{defer{working=false};do{preview=try await model.bridge.send(["op":"folderBatchPreview","token":token,"ids":ids,"text":text],as:BatchPreview.self)}catch{model.error=error.localizedDescription}}}
    private func export(_ preview:BatchPreview){
        let panel=NSOpenPanel();panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.canCreateDirectories=true
        guard panel.runModal() == .OK,let url=panel.url else{return}
        working=true;Task{defer{working=false};do{let result=try await model.bridge.send(["op":"folderBatchExport","token":preview.token,"path":url.path],as:FolderBatchResult.self);exported=result.path}catch{model.error=error.localizedDescription}}
    }
}
struct FolderBatchResult:Codable {let path:String,count:Int}
