import AppKit

// PKHeX's tiled-image renderer returns little-endian BGRA, including opaque alpha.
// Use an explicit Core Graphics pixel layout; NSBitmapImageRep's bitmap flags
// otherwise reinterpret the first color channel as alpha on this platform.
enum CGearRaster {
    static let width = 256, height = 192
    static func bitmap(bgra: Data) throws -> NSBitmapImageRep {
        guard bgra.count == width * height * 4,
              let provider = CGDataProvider(data: bgra as CFData),
              let image = CGImage(width: width, height: height, bitsPerComponent: 8,
                  bitsPerPixel: 32, bytesPerRow: width * 4,
                  space: CGColorSpaceCreateDeviceRGB(),
                  bitmapInfo: CGBitmapInfo(rawValue: CGBitmapInfo.byteOrder32Little.rawValue | CGImageAlphaInfo.premultipliedFirst.rawValue),
                  provider: provider, decode: nil, shouldInterpolate: false, intent: .defaultIntent)
        else { throw CocoaError(.fileReadCorruptFile) }
        return NSBitmapImageRep(cgImage: image)
    }
    static func pixels(png: Data) throws -> Data {
        guard let image = NSBitmapImageRep(data: png)?.cgImage,
              image.width == width, image.height == height else { throw ImageError.dimensions }
        var bytes = Data(count: width * height * 4)
        let drawn = bytes.withUnsafeMutableBytes { buffer -> Bool in
            guard let context = CGContext(data: buffer.baseAddress, width: width, height: height,
                bitsPerComponent: 8, bytesPerRow: width * 4, space: CGColorSpaceCreateDeviceRGB(),
                bitmapInfo: CGBitmapInfo.byteOrder32Little.rawValue | CGImageAlphaInfo.premultipliedFirst.rawValue)
            else { return false }
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
            return true
        }
        guard drawn else { throw CocoaError(.fileReadCorruptFile) }
        return bytes
    }
    private enum ImageError: LocalizedError {
        case dimensions
        var errorDescription: String? { "Choose a PNG that is exactly 256 × 192 pixels." }
    }
}
