import SwiftUI

struct TrainingEntry:Codable,Identifiable {let id:Int, name:String, group:String, completed:Bool, enabled:Bool}
struct SuperTrainingData:Codable {
    let entries:[TrainingEntry], distribution:[TrainingEntry], native:Bool, unlocked:Bool, complete:Bool, bag:Int, hits:Int, bags:[Choice]
}
struct SuperTrainingView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var distribution=false
    private func edit(_ values:[String:Any]) {Task{await model.command(values.merging(["op":"superTrainingSet"]){_,new in new},status:"Super Training updated — Set to Slot to keep it")}}
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Text("Super Training").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            if let data=model.state.superTraining {
                Text("Edit completion medals and training history. These records do not increase EVs. Use Set to Slot after editing.").font(.callout).foregroundStyle(.secondary)
                if data.native {
                    HStack {
                        Toggle("Secret Training unlocked",isOn:Binding(get:{data.unlocked},set:{edit(["mode":"unlocked","value":$0])})).toggleStyle(.checkbox)
                        Spacer()
                        Toggle("Supremely Trained",isOn:Binding(get:{data.complete},set:{edit(["mode":"complete","value":$0])})).toggleStyle(.checkbox).disabled(!data.unlocked)
                    }
                    Text("Turning off Secret Training also clears its 12 medals and Supremely Trained status.").font(.caption).foregroundStyle(.secondary)
                    HStack {
                        Picker("Training bag",selection:Binding(get:{data.bag},set:{edit(["mode":"bag","bag":$0,"hits":data.hits])})) {
                            ForEach(data.bags){Text($0.label).tag(Int($0.value)!)}
                            if !data.bags.contains(where:{$0.value==String(data.bag)}) {Text("Stored bag \(data.bag)").tag(data.bag)}
                        }
                        Stepper("Hits: \(data.hits)",value:Binding(get:{data.hits},set:{edit(["mode":"bag","bag":data.bag,"hits":$0])}),in:0...255).frame(width:160)
                    }
                } else {Text("Generation 7 retains medal records from earlier games. Training bags and Secret Training switches are not exposed here.").font(.caption).foregroundStyle(.secondary)}
                Picker("Regimens",selection:$distribution){Text("Regular & Secret (30)").tag(false);Text("Distribution (6)").tag(true)}.pickerStyle(.segmented)
                ScrollView {
                    LazyVStack(spacing:0) {
                        ForEach(distribution ? data.distribution : data.entries){entry in
                            HStack {
                                VStack(alignment:.leading,spacing:4){Text(entry.name).font(.callout);Text(entry.group).font(.caption).foregroundStyle(.secondary)}
                                Spacer()
                                Toggle(entry.name,isOn:Binding(get:{entry.completed},set:{edit(["mode":"regimen","id":entry.id,"distribution":distribution,"value":$0])})).labelsHidden().toggleStyle(.checkbox).disabled(!entry.enabled)
                            }.padding(.vertical,10)
                            Divider().opacity(0.4)
                        }
                    }
                }
                HStack {
                    Button("Clear Medals"){edit(["mode":"clear"])}
                    Spacer()
                    Menu("Complete Medals") {
                        Button("Regular & Secret"){edit(["mode":"all","distribution":false])}
                        Button("Include Distribution Events"){edit(["mode":"all","distribution":true])}
                    }
                }
                Text("Undo is available in the Edit menu. Distribution-event medals may fail legality checks.").font(.caption).foregroundStyle(.secondary)
            } else {ContentUnavailableView("Super Training Unavailable",systemImage:"medal")}
        }.padding(24).frame(width:660,height:660).disabled(model.busy)
    }
}
