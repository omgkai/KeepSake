import SwiftUI
struct DonutRangeInfo:Codable {let revision:Int,count:Int,flavors:[Choice]}
struct DonutRangeSheet:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var info:DonutRangeInfo?
    @State private var start=1
    @State private var end=999
    @State private var flavors:Set<String>=[]
    @State private var error=""
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Label("Create a donut collection",systemImage:"circle.circle.fill").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}}
            Text("Use PKHeX’s shiny-template generator with your chosen slot range and flavor pool. Existing donuts in that range will be replaced. Undo restores the whole collection.").foregroundStyle(.secondary)
            if let info {
                HStack {Stepper("First slot: \(start)",value:$start,in:1...info.count);Stepper("Last slot: \(end)",value:$end,in:1...info.count)}
                HStack {Text("Flavor pool").font(.headline);Spacer();Button("All"){flavors=Set(info.flavors.map(\.value))};Button("Clear"){flavors=[]}}
                List(info.flavors){f in Toggle(f.label,isOn:Binding(get:{flavors.contains(f.value)},set:{if $0{flavors.insert(f.value)}else{flavors.remove(f.value)}})).toggleStyle(.checkbox)}
                HStack {Text("\(flavors.count) flavors · \(max(0,end-start+1)) slots").font(.caption);Spacer();Button("Replace Selected Range"){Task{await model.command(["op":"donutRangeGenerate","revision":info.revision,"start":start-1,"end":end,"flavors":Array(flavors)],status:"Donut range generated — Undo is available");if model.error==nil{dismiss()}}}.buttonStyle(.borderedProminent).disabled(start>end || flavors.isEmpty || model.busy)}
            } else {ProgressView()}
            if !error.isEmpty{Text(error).foregroundStyle(.orange)}
        }.padding(24).frame(width:660,height:630).task{do{let value=try await model.bridge.send(["op":"donutRangeInfo"],as:DonutRangeInfo.self);info=value;end=value.count;flavors=Set(value.flavors.map(\.value))}catch{self.error=error.localizedDescription}}
    }
}
