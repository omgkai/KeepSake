import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct CGearPixels:Codable {let width:Int,height:Int,pixels:String,empty:Bool}
struct CGearPreview:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var bitmap:NSBitmapImageRep?
    @State private var empty=true
    @State private var loading=true
    @State private var error:String?
    @State private var revision=0
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack{Label("C-Gear background",systemImage:"paintpalette.fill").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Make Unova your own. Import a 256 × 192 PNG with up to 16 colors and 255 unique 8 × 8 tiles. KeepSake checks the image before changing your save.").foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
            Group {
                if loading {ProgressView()}
                else if empty {ContentUnavailableView("No background yet",systemImage:"photo",description:Text("Import a PNG or a C-Gear file to get started."))}
                else if let cg=bitmap?.cgImage {Image(decorative:cg,scale:1).resizable().interpolation(.none).scaledToFit().padding(12)}
            }.frame(maxWidth:.infinity).frame(height:320).background(Color(nsColor:.underPageBackgroundColor),in:RoundedRectangle(cornerRadius:18))
            if let error{Text(error).font(.callout).foregroundStyle(.red).textSelection(.enabled)}
            HStack {Button("Import PNG…"){importPNG()}.buttonStyle(.borderedProminent);Button("Export PNG…"){exportPNG()}.disabled(bitmap==nil || empty);Spacer();Text("Changes support Undo").font(.caption).foregroundStyle(.secondary)}
        }.padding(24).frame(width:620).disabled(model.busy || loading).task{await load()}
    }
    private func load()async {
        loading=true;defer{loading=false}
        do {let data=try await model.bridge.send(["op":"cgearImage"],as:CGearPixels.self);guard data.width==256,data.height==192,let bytes=Data(base64Encoded:data.pixels),bytes.count==256*192*4 else{throw CocoaError(.fileReadCorruptFile)}
            bitmap=try CGearRaster.bitmap(bgra:bytes);empty=data.empty;revision=model.state.revision
        }catch{self.error=error.localizedDescription}
    }
    private func importPNG() {
        let panel=NSOpenPanel();panel.allowedContentTypes=[.png];guard panel.runModal() == .OK,let url=panel.url else{return}
        do {let bytes=try CGearRaster.pixels(png:Data(contentsOf:url))
            Task{await model.command(["op":"cgearImageSet","revision":revision,"pixels":bytes.base64EncodedString()],status:"Updated C-Gear background");if let issue=model.error{error=issue}else{error=nil;await load()}}
        }catch{self.error=error.localizedDescription}
    }
    private func exportPNG() {
        guard let data=bitmap?.representation(using:.png,properties:[:])else{return};let panel=NSSavePanel();panel.allowedContentTypes=[.png];panel.nameFieldStringValue="C-Gear.png";guard panel.runModal() == .OK,let url=panel.url else{return}
        Task{do{_=try await model.bridge.send(["op":"cgearPngExport","path":url.path,"png":data.base64EncodedString()],as:PathResult.self);model.status="Exported C-Gear PNG"}catch{self.error=error.localizedDescription}}
    }
}
