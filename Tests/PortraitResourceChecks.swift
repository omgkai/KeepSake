import Foundation
@main struct Checks {
 static func main() throws {
  let root=URL(fileURLWithPath:CommandLine.arguments[1]);let manifest=try JSONDecoder().decode(PortraitManifest.self,from:Data(contentsOf:root.appendingPathComponent("manifest.json")))
  var checks=0
  func expect(_ pass:Bool,_ why:String){checks+=1;precondition(pass,why)}
  func resolve(_ name:String,_ identity:String?=nil)->String?{PortraitResourceResolver.resolve(sprite:name,identity:identity,manifest:manifest,in:root)?.path.replacingOccurrences(of:root.path+"/",with:"")}
  for species in 1...1025 {expect(resolve("b_\(species)") != nil,"Base species \(species)");expect(resolve("b_\(species)s")?.hasPrefix("shiny/")==true,"Shiny species \(species)")}
  for (key,file) in manifest.identities {expect(FileManager.default.fileExists(atPath:root.appendingPathComponent(file).path),"Missing identity \(key)")}
  expect(resolve("b_25","Gen9:25:0:1:0:0")=="female/25.png","Female Pikachu portrait")
  expect(resolve("b_25s","Gen9:25:0:1:0:1")=="shiny/female/25.png","Shiny female Pikachu")
  expect(resolve("b_892","Gen8:892:1:0:0:0")=="10191.png","Rapid Strike identity survives shared pixel icon")
  expect(resolve("b_892","Gen8:892:0:0:0:0")=="892.png","Single Strike")
  expect(resolve("b_666","Gen6:666:0:0:0:0")=="666-icy-snow.png","Vivillon form 0 is not API default")
  expect(resolve("b_869-8-6s","Gen9:869:8:1:6:1")=="shiny/869-rainbow-swirl-ribbon-sweet.png","Alcremie cream/sweet/shiny")
  expect(resolve("b_25-1c","Gen6:25:1:1:0:0")==nil,"Unavailable cosplay retains pixel fallback")
  expect(resolve("b_25","Gen9:25:250:0:0:0")==nil,"Unknown form cannot use base portrait")
  expect(resolve("b_1","../1")==nil,"Invalid identity")
  expect(resolve("a_25s")=="shiny/25.png","Theme alias keeps shiny")
  expect(resolve("b_25","Gen1:25:0:0:0:0")=="25.png","Early-generation identity")
  print("PASS \(checks) portrait coverage, variant identity and fallback assertions")
 }
}
