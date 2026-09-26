import SwiftUI
import CoreImage
import UniformTypeIdentifiers

struct PokemonQRData:Codable {let payload:String,lines:[String],format:String}
struct PokemonQRView:View {
    var giftMode = false
    var giftID: Int? = nil
    @State private var giftFormat = "wc6"
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var data:PokemonQRData?
    @State private var image:NSImage?
    @State private var error:String?
    var body:some View {
        VStack(spacing:16) {
            HStack {Label(giftMode ? "Mystery Gift QR" : "Pokémon QR",systemImage:"qrcode").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            if let image {Image(nsImage:image).interpolation(.none).resizable().scaledToFit().frame(width:300,height:300).padding(12).background(.white,in:RoundedRectangle(cornerRadius:14))}
            if let data{ForEach(data.lines,id:\.self){Text($0).font(.caption).textSelection(.enabled)}}
            if let error{Text(error).font(.callout).foregroundStyle(.red)}
            if giftMode { Picker("Original card format",selection:$giftFormat) { ForEach(["pgt","pcd","pgf","wc6","wc6full","wc7","wc7full","wr7","wb7","wb7full","wc8","wc8full","wb8","wa8","wc9","wa9"],id:\.self) { Text($0).tag($0) } }; Text("Import adds the card to this session’s library for review. Choose its original format; some formats have identical sizes. No website is contacted.").font(.caption).foregroundStyle(.secondary) }
            else { Text("Uses PKHeX’s QR format, including the Sun/Moon scanner payload for .pk7 files. Other games may need a compatible editor to read the code. QR data is handled locally.").font(.caption).foregroundStyle(.secondary) }
            HStack {
                Button("Import QR Image…"){importImage()}
                Button("Paste QR Text"){if let text=NSPasteboard.general.string(forType:.string),let bytes=text.data(using:.isoLatin1){prepare(bytes)}else{error="The clipboard does not contain QR text."}}
                Spacer()
                Button("Save PNG…"){saveImage()}.disabled(image==nil)
            }
        }.padding(24).frame(width:650,height:590).task{await load()}
    }
    private func load()async {
        do {
            if giftMode && giftID == nil { return }
            var request:[String:Any] = ["op":giftMode ? "giftQR" : "pokemonQR"]
            if let giftID { request["id"]=giftID }
            let result=try await model.bridge.send(request,as:PokemonQRData.self)
            if giftMode { giftFormat=result.format }
            guard let bytes=Data(base64Encoded:result.payload),let filter=CIFilter(name:"CIQRCodeGenerator") else{throw PokemonQRCodec.failure("Could not create the QR code.")}
            filter.setValue(bytes,forKey:"inputMessage");filter.setValue("M",forKey:"inputCorrectionLevel")
            guard let output=filter.outputImage else{throw PokemonQRCodec.failure("Could not render the QR code.")}
            let padded=output.composited(over:CIImage(color:.white).cropped(to:output.extent.insetBy(dx:-4,dy:-4)))
            let scaled=padded.transformed(by:CGAffineTransform(scaleX:6,y:6))
            guard let cg=CIContext().createCGImage(scaled,from:scaled.extent) else{throw PokemonQRCodec.failure("Could not render the QR code.")}
            image=NSImage(cgImage:cg,size:NSSize(width:cg.width,height:cg.height));data=result
        }catch{self.error=error.localizedDescription}
    }
    private func saveImage(){
        guard let image,let tiff=image.tiffRepresentation,let png=NSBitmapImageRep(data:tiff)?.representation(using:.png,properties:[:]) else{return}
        let panel=NSSavePanel();panel.allowedContentTypes=[.png];panel.nameFieldStringValue=giftMode ? "Mystery Gift QR.png" : "Pokemon QR.png"
        if panel.runModal() == .OK,let url=panel.url{do{try png.write(to:url,options:.atomic)}catch{self.error=error.localizedDescription}}
    }
    private func importImage(){
        let panel=NSOpenPanel();panel.allowedContentTypes=[.png,.jpeg,.tiff];panel.allowsMultipleSelection=false
        guard panel.runModal() == .OK,let url=panel.url else{return}
        do {
            let size=(try url.resourceValues(forKeys:[.fileSizeKey])).fileSize ?? 0
            guard size<=20000000,let input=CIImage(contentsOf:url),input.extent.width*input.extent.height<=40000000 else{throw PokemonQRCodec.failure("Choose a QR image under 20 MB and 40 megapixels.")}
            let detector=CIDetector(ofType:CIDetectorTypeQRCode,context:CIContext(),options:[CIDetectorAccuracy:CIDetectorAccuracyHigh])
            let codes=(detector?.features(in:input) ?? []).compactMap{$0 as? CIQRCodeFeature}
            guard codes.count==1,let descriptor=codes[0].symbolDescriptor else{throw PokemonQRCodec.failure("Choose an image containing one readable QR code.")}
            prepare(try PokemonQRCodec.payload(descriptor))
        }catch{self.error=error.localizedDescription}
    }
    private func prepare(_ bytes:Data){
        if giftMode {
            guard !model.busy else { return }; model.busy=true
            Task { defer { model.busy=false }; do { model.gifts=try await model.bridge.send(["op":"giftQRImport","payload":bytes.base64EncodedString(),"format":giftFormat],as:[GiftEntry].self); model.status="QR card added to the gift library"; dismiss() } catch { self.error=error.localizedDescription } }; return
        }
        guard !model.busy,model.confirmDiscard(all:false) else{return}
        Task{await model.command(["op":"pokemonQRImport","payload":bytes.base64EncodedString()],status:"QR Pokémon prepared — review legality before Set to Slot");if model.error==nil{dismiss()}}
    }
}
