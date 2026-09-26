import AppKit

@main struct CGearRasterChecks {
    static func main() throws {
        var pixels = Data()
        for y in 0..<192 { for _ in 0..<256 { pixels.append(contentsOf: y < 96 ? [0,0,255,255] : [255,0,0,255]) } }
        let bitmap = try CGearRaster.bitmap(bgra: pixels)
        let top = bitmap.colorAt(x: 0, y: 0)!.usingColorSpace(.deviceRGB)!
        let bottom = bitmap.colorAt(x: 255, y: 191)!.usingColorSpace(.deviceRGB)!
        precondition(top.redComponent > 0.99 && top.blueComponent < 0.01 && top.alphaComponent == 1, "Top stays opaque red")
        precondition(bottom.blueComponent > 0.99 && bottom.redComponent < 0.01 && bottom.alphaComponent == 1, "Bottom stays opaque blue")
        let png = bitmap.representation(using: .png, properties: [:])!
        let restored = try CGearRaster.pixels(png: png)
        precondition(restored == pixels, "PNG export/import preserves orientation, channels and alpha")
        do { _ = try CGearRaster.bitmap(bgra: Data(count: 4)); fatalError("Malformed pixels accepted") } catch {}
        do { _ = try CGearRaster.pixels(png: Data()); fatalError("Malformed PNG accepted") } catch {}
        print("PASS C-Gear native BGRA color/alpha, PNG roundtrip/orientation, malformed input rejection")
    }
}
