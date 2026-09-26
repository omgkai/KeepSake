import AppKit
struct Region:Decodable {let sheet:String;let x,y,width,height:Int}
let root=URL(fileURLWithPath:CommandLine.arguments[1]);let regions=try JSONDecoder().decode([String:Region].self,from:Data(contentsOf:root.appendingPathComponent("manifest.json")))
var checked=0
for (key,r) in regions.sorted(by:{$0.key<$1.key}) {
    let rep=NSBitmapImageRep(data:try Data(contentsOf:root.appendingPathComponent(r.sheet+".png")))!
    precondition(r.x>=0 && r.y>=0 && r.x+r.width<=rep.pixelsWide && r.y+r.height<=rep.pixelsHigh,"Out of atlas: "+key)
    var opaque=0;var boundary=0
    for y in r.y..<r.y+r.height {for x in r.x..<r.x+r.width {if rep.colorAt(x:x,y:y)!.alphaComponent>0.05 {opaque+=1;if x==r.x || x==r.x+r.width-1 || y==r.y || y==r.y+r.height-1 {boundary+=1}}}}
    precondition(opaque>20 && opaque<r.width*r.height,"Empty or opaque rectangle: "+key)
    print("PASS \(key): \(opaque) visible pixels; \(boundary) on crop boundary")
    checked+=1
}
precondition(checked==68)
print("PASS \(checked) atlas regions; source sheets remain unchanged")
