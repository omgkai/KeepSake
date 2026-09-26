import SwiftUI
import AppKit

private struct ChatterAudio:Decodable {let wav:String}
struct ChatterPlayer:View {
    @EnvironmentObject var model:EditorModel
    @State private var sound:NSSound?
    @State private var peaks:[Double]=[]
    @State private var loading=true
    var body:some View {
        VStack(alignment:.leading,spacing:14) {
            HStack(alignment:.center,spacing:4) {
                ForEach(Array(peaks.enumerated()),id:\.offset){_,peak in
                    Capsule().fill(Color.accentColor.gradient).frame(maxWidth:.infinity).frame(height:max(4,48*peak))
                }
            }.frame(height:52).accessibilityLabel("Saved Chatter recording waveform")
            HStack {
                Button {sound?.stop();sound?.play()} label:{Label("Play Recording",systemImage:"play.fill")}.disabled(sound==nil)
                Button {sound?.stop()} label:{Label("Stop",systemImage:"stop.fill")}.disabled(sound==nil)
                Spacer()
                Button {exportWave()} label:{Label("Export WAV…",systemImage:"waveform.badge.plus")}.disabled(loading)
            }
            Text("1 second · Original Nintendo DS recording · 2 kHz mono").font(.caption).foregroundStyle(.secondary)
        }.padding(16).background(.tint.opacity(0.08),in:RoundedRectangle(cornerRadius:16))
            .task {
                do {
                    let result=try await model.bridge.send(["op":"chatterAudio"],as:ChatterAudio.self)
                    try Task.checkCancellation()
                    guard let data=Data(base64Encoded:result.wav),data.count==2044 else{throw CocoaError(.fileReadCorruptFile)}
                    sound=NSSound(data:data)
                    let samples=Array(data.dropFirst(44))
                    peaks=(0..<50).map{i in samples[(i*40)..<((i+1)*40)].map{abs(Double($0)-128)/128}.max() ?? 0}
                    loading=false
                }catch is CancellationError{}catch{model.error=error.localizedDescription;loading=false}
            }.onDisappear{sound?.stop()}
    }
    private func exportWave() {
        let panel=NSSavePanel();panel.nameFieldStringValue="Chatter.wav"
        guard panel.runModal() == .OK,let url=panel.url else{return}
        Task {do{_ = try await model.bridge.send(["op":"extraExport","kind":"chatter","id":"recording","format":"wav","path":url.path],as:PathResult.self);model.status="Exported Chatter WAV"}catch{model.error=error.localizedDescription}}
    }
}
