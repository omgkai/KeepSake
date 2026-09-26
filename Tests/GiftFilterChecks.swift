import Foundation
@main struct Checks {
 static func main() throws {
  let entries=try JSONDecoder().decode([GiftEntry].self,from:Data(contentsOf:URL(fileURLWithPath:CommandLine.arguments[1])))
  var count=0
  func check(_ f:GiftFilters,_ expected:(GiftEntry)->Bool){precondition(entries.filter(f.matches).map(\.id)==entries.filter(expected).map(\.id));count+=1}
  check(GiftFilters()){_ in true}
  for language in GiftEntry.languageNames.keys {var f=GiftFilters();f.language=language;check(f){$0.languages.contains(language)}}
  for generation in 4...9 {for comparison in ["exact","before","after"] {var f=GiftFilters();f.generation=generation;f.comparison=comparison;check(f){comparison=="exact" ? $0.generation==generation:comparison=="before" ? $0.generation<=generation:$0.generation>=generation}}}
  for rule in ["Never","Always","Random"] {var f=GiftFilters();f.shiny=rule;check(f){$0.shinyRule==rule}}
  for g in entries.prefix(30) {var f=GiftFilters();f.species=String(g.species);f.heldItem=g.heldItem;f.moves=Array(g.moves.map(String.init).prefix(4));f.language=g.languages.first ?? 0;check(f){$0.species==g.species && $0.heldItem==g.heldItem && Set(g.moves).isSubset(of:Set($0.moves)) && (f.language==0 || $0.languages.contains(f.language))}}
  var f=GiftFilters();f.language = -2;check(f){$0.languages.count>1};f.language = -1;check(f){$0.entity && !$0.languageKnown}
  f=GiftFilters();f.egg="egg";check(f){$0.entity && $0.egg};f=GiftFilters();f.kind="items";check(f){!$0.entity};f=GiftFilters();f.source="Folder";check(f){$0.source=="Folder"}
  f=GiftFilters();f.search="Pikachu";precondition(entries.filter(f.matches).contains{$0.species==25});precondition(f.active);precondition(!GiftFilters().active)
  print("PASS \(count) filter comparisons across \(entries.count) cards; search and reset state")
 }
}
