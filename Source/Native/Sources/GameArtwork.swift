import SwiftUI
import AppKit

// Add the same crisp silhouette as the original item sprites without altering source art.
struct GameArtwork:View {
    let image:NSImage
    let whiteOutline:Bool
    private let edges:[CGSize]=[
        CGSize(width:-1,height:-1),CGSize(width:0,height:-1),CGSize(width:1,height:-1),
        CGSize(width:-1,height:0),CGSize(width:1,height:0),
        CGSize(width:-1,height:1),CGSize(width:0,height:1),CGSize(width:1,height:1)
    ]
    var body:some View {
        ZStack {
            if whiteOutline {
                ForEach(edges.indices,id:\.self) {index in
                    Image(nsImage:image).renderingMode(.template).resizable()
                        .interpolation(.none).scaledToFit().foregroundStyle(.white)
                        .offset(edges[index])
                }
            }
            Image(nsImage:image).resizable().interpolation(.none).scaledToFit()
        }.padding(whiteOutline ? 1 : 0).accessibilityHidden(true)
    }
}
