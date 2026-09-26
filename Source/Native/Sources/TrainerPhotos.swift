import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct TrainerPhoto:Decodable,Identifiable {
    let id,title:String
    let width,height:Int
    let bgra:String
    var image:NSImage? {
        guard width>0,height>0,width<=2048,height<=2048,let bytes=Data(base64Encoded:bgra),bytes.count==width*height*4,
              let provider=CGDataProvider(data:bytes as CFData),
              let image=CGImage(width:width,height:height,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:width*4,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGBitmapInfo(rawValue:CGBitmapInfo.byteOrder32Little.rawValue | CGImageAlphaInfo.first.rawValue),provider:provider,decode:nil,shouldInterpolate:true,intent:.defaultIntent) else {return nil}
        return NSImage(cgImage:image,size:NSSize(width:width,height:height))
    }
}
struct TrainerPhotosView:View {
    @EnvironmentObject private var model:EditorModel
    @State private var photos:[TrainerPhoto]=[]
    @State private var loading=true
    var body:some View {
        ScrollView {
            if loading {ProgressView().padding(40)}
            else if photos.isEmpty {ContentUnavailableView("No saved photos",systemImage:"photo.on.rectangle.angled",description:Text("Photos appear for supported Scarlet, Violet and Z-A saves that contain recorded images.")).padding(32)}
            else {VStack(spacing:22) {ForEach(photos) {photo in
                VStack(spacing:12) {
                    HStack {Text(photo.title).font(.headline);Spacer();Button("Export PNG…"){save(photo)}}
                    if let image=photo.image {Image(nsImage:image).resizable().scaledToFit().frame(maxHeight:280).clipShape(RoundedRectangle(cornerRadius:14))}
                    Text("\(photo.width) × \(photo.height)").font(.caption).foregroundStyle(.secondary)
                }.padding(18).background(.quaternary.opacity(0.3),in:RoundedRectangle(cornerRadius:18))
            }}.padding(24)}
        }.task {do{photos=try await model.bridge.send(["op":"trainerPhotos"],as:[TrainerPhoto].self)}catch{model.error=error.localizedDescription};loading=false}
    }
    private func save(_ photo:TrainerPhoto) {
        guard let tiff=photo.image?.tiffRepresentation,let png=NSBitmapImageRep(data:tiff)?.representation(using:.png,properties:[:]) else{return}
        let panel=NSSavePanel();panel.allowedContentTypes=[.png];panel.nameFieldStringValue=photo.title+".png"
        if panel.runModal() == .OK,let url=panel.url {do{try png.write(to:url,options:.atomic)}catch{model.error=error.localizedDescription}}
    }
}
