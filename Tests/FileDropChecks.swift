import Foundation
import CoreTransferable
import UniformTypeIdentifiers
@main struct FileDropChecks {
    static func main() async throws {
        let root=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer{try? FileManager.default.removeItem(at:root)}
        let save=root.appendingPathComponent("Pokémon save with spaces")
        try Data([1,2,3]).write(to:save)
        let selected=try FileDropSelection.file(from:[save]);precondition(selected==save)
        let empty=root.appendingPathComponent("empty");try Data().write(to:empty)
        let large=root.appendingPathComponent("oversized");FileManager.default.createFile(atPath:large.path,contents:nil)
        let handle=try FileHandle(forWritingTo:large);try handle.truncate(atOffset:64*1024*1024+1);try handle.close()
        for urls in [[],[save,save],[root],[empty],[large],[URL(string:"https://example.com/save")!],[root.appendingPathComponent("missing")]] {
            do{_ = try FileDropSelection.file(from:urls);fatalError("Invalid drop accepted")}catch{}
        }
        let provider=NSItemProvider()
        provider.registerDataRepresentation(forTypeIdentifier:UTType.fileURL.identifier,visibility:.all) { completion in
            completion(save.absoluteString.data(using:.utf8),nil);return nil
        }
        let transferred:FileDropItem=try await withCheckedThrowingContinuation { continuation in
            _=provider.loadTransferable(type:FileDropItem.self){continuation.resume(with:$0)}
        }
        let decoded=try FileDropSelection.file(from:[transferred.url]);precondition(decoded==save)
        print("PASS Finder-style public.file-url payload decodes through the explicit file Transferable used by the window drop destination")
        let original=try Data(contentsOf:save);precondition(original==Data([1,2,3]))
        print("PASS local file URLs, extensionless/Unicode paths, multiple/folder/empty/oversized/remote/missing rejection, original preservation")
    }
}
