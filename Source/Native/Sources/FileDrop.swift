import Foundation
import CoreTransferable
import UniformTypeIdentifiers

/// Keep opening rules shared between Finder drops, Open, and file URL events.
enum FileDropSelection {
    static func file(from urls: [URL]) throws -> URL {
        guard urls.count == 1 else { throw DropError("Drop one save or Pokémon file at a time.") }
        let url = urls[0]
        guard url.isFileURL else { throw DropError("Drop a local file from Finder.") }
        let values = try url.resourceValues(forKeys: [.isRegularFileKey, .fileSizeKey])
        guard values.isRegularFile == true else { throw DropError("Choose a file, rather than a folder or application.") }
        guard let size = values.fileSize, size > 0, size <= 64 * 1024 * 1024 else {
            throw DropError("Choose a nonempty save or Pokémon file up to 64 MB.")
        }
        return url
    }
    private struct DropError: LocalizedError {
        let message: String
        init(_ message: String) { self.message = message }
        var errorDescription: String? { message }
    }
}

/// Finder advertises public.file-url, which Foundation.URL's built-in
/// Transferable does not import on every supported macOS release.
struct FileDropItem: Transferable {
    let url:URL
    static var transferRepresentation: some TransferRepresentation {
        DataRepresentation(importedContentType:.fileURL) { data in
            guard data.count<=65536,let url=URL(dataRepresentation:data,relativeTo:nil),url.isFileURL else {
                throw CocoaError(.fileReadUnsupportedScheme)
            }
            return FileDropItem(url:url)
        }
    }
}

/// A box cell accepts the existing internal slot payload and Finder file URLs.
struct SlotDropItem:Transferable {
    let payload:String?
    let url:URL?
    static var transferRepresentation:some TransferRepresentation {
        DataRepresentation(importedContentType:.fileURL){data in
            guard data.count<=65536,let url=URL(dataRepresentation:data,relativeTo:nil),url.isFileURL else{throw CocoaError(.fileReadUnsupportedScheme)}
            return SlotDropItem(payload:nil,url:url)
        }
        ProxyRepresentation(importing:{(value:String) in SlotDropItem(payload:value,url:nil)})
    }
}

import SwiftUI

// Explicit item-provider decoding supports Finder and older macOS drag pasteboards.
struct PokemonSlotDrop:ViewModifier {
    @EnvironmentObject var model:EditorModel
    let slot:Slot
    func body(content:Content)->some View {
        content.onDrop(of:slot.party ? [UTType.text]:[UTType.text,UTType.fileURL],isTargeted:nil){providers in
            guard providers.count==1,let provider=providers.first,!model.busy else{return false}
            let box=model.state.box,revision=model.state.revision,session=model.state.dragSession
            if !slot.party,provider.hasItemConformingToTypeIdentifier(UTType.fileURL.identifier) {
                provider.loadDataRepresentation(forTypeIdentifier:UTType.fileURL.identifier){data,_ in
                    guard let data,data.count<=65536,let url=URL(dataRepresentation:data,relativeTo:nil),url.isFileURL else{return}
                    DispatchQueue.main.async {
                        guard model.state.revision==revision,model.state.dragSession==session else{model.error="The workspace changed. Drop the file again.";return}
                        _=model.dropPokemonFile(url,slot:slot.index)
                    }
                };return true
            }
            guard provider.canLoadObject(ofClass:NSString.self) else{return false}
            provider.loadObject(ofClass:NSString.self){value,_ in
                guard let payload=value as? String else{return}
                DispatchQueue.main.async{_ = model.dropBoxSlot(payload,box:box,slot:slot.index,party:slot.party)}
            };return true
        }
    }
}
