import SwiftUI
import AppKit
import UniformTypeIdentifiers

/// The game's 24 × 20 canvas: four pixels per byte, low two bits first.
struct DotArtistCanvas: View {
    @Binding var value: String
    @State private var brush = 3
    @State private var message = ""
    private let shades: [Int] = [248, 168, 88, 8]
    private var pixels: [Int] { value.map { Int(String($0)) ?? 0 } }
    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 10) {
                Text("Ink").font(.headline)
                ForEach(0..<4) { shade in
                    Button { brush = shade } label: {
                        Circle().fill(color(shade)).frame(width: 28, height: 28)
                            .overlay(Circle().stroke(brush == shade ? Color.accentColor : .secondary.opacity(0.3), lineWidth: brush == shade ? 3 : 1))
                    }.buttonStyle(.plain).help("Shade \(shade + 1)").accessibilityLabel("Shade \(shade + 1)")
                }
                Spacer()
                Button("Fill") { value = String(repeating: String(brush), count: 480) }
                Button("Clear") { value = String(repeating: "0", count: 480) }
            }
            Canvas { context, size in
                let data = pixels
                for y in 0..<20 { for x in 0..<24 {
                    let index = y * 24 + x
                    let rect = CGRect(x: CGFloat(x)*size.width/24, y: CGFloat(y)*size.height/20, width: size.width/24, height: size.height/20)
                    context.fill(Path(rect), with: .color(color(index < data.count ? data[index] : 0)))
                    context.stroke(Path(rect), with: .color(.gray.opacity(0.2)), lineWidth: 0.5)
                } }
            }.frame(width: 480, height: 400)
                .gesture(DragGesture(minimumDistance: 0).onChanged { event in
                    let x = Int(floor(event.location.x/20)), y = Int(floor(event.location.y/20))
                    guard (0..<24).contains(x), (0..<20).contains(y) else { return }
                    var data = Array(value); guard data.count == 480 else { return }
                    data[y*24+x] = Character(String(brush)); value = String(data)
                })
                .accessibilityLabel("Dot Artist, 24 by 20 pixel drawing canvas")
                .frame(maxWidth: .infinity)
            HStack {
                Button("Import Image…", action: importImage)
                Button("Export PNG…", action: exportImage)
                Spacer()
                Text("24 × 20 · four shades").font(.caption).foregroundStyle(.secondary)
            }
            if !message.isEmpty { Text(message).font(.caption).foregroundStyle(.secondary) }
        }
    }
    private func color(_ shade: Int) -> Color { Color(white: Double(shades[max(0,min(3,shade))])/255) }
    private func importImage() {
        let panel = NSOpenPanel(); panel.allowedContentTypes = [.png, .bmp, .tiff]; panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let data = try Data(contentsOf: url)
            guard let image = NSBitmapImageRep(data: data), image.pixelsWide == 24, image.pixelsHigh == 20 else { throw DotArtistError("Choose an image exactly 24 × 20 pixels.") }
            var brightness: [Int] = []
            for y in 0..<20 { for x in 0..<24 {
                guard let rgb = image.colorAt(x: x, y: y)?.usingColorSpace(.deviceRGB) else { throw DotArtistError("The image’s colors could not be read.") }
                // Match Windows Color.GetBrightness (HSL lightness).
                brightness.append(Int(255 * (max(rgb.redComponent,rgb.greenComponent,rgb.blueComponent) + min(rgb.redComponent,rgb.greenComponent,rgb.blueComponent))/2))
            } }
            let unique = Set(brightness)
            guard unique.count <= 4 else { throw DotArtistError("Use up to four brightness levels. Reduce the image’s colors before importing.") }
            value = brightness.map { b in String((0..<4).min { abs(shades[$0]-b) < abs(shades[$1]-b) }!) }.joined()
            message = "Image imported. Save Changes to apply it to your Pokétch."
        } catch { message = error.localizedDescription }
    }
    private func exportImage() {
        let panel = NSSavePanel(); panel.allowedContentTypes = [.png]; panel.nameFieldStringValue = "Poketch-Dot-Artist.png"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        guard let image = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: 24, pixelsHigh: 20, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 96, bitsPerPixel: 32) else { return }
        let data = pixels
        for y in 0..<20 { for x in 0..<24 { let i = y*24+x; image.setColor(NSColor(white: CGFloat(shades[i < data.count ? data[i] : 0])/255, alpha: 1), atX: x, y: y) } }
        do { guard let png = image.representation(using: .png, properties: [:]) else { throw DotArtistError("Could not encode this image.") }; try png.write(to: url, options: .atomic); message = "Exported a 24 × 20 PNG." } catch { message = error.localizedDescription }
    }
}
private struct DotArtistError: LocalizedError { let text: String; init(_ text: String) { self.text = text }; var errorDescription: String? { text } }
