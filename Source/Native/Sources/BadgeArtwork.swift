import SwiftUI
import AppKit

/// Source sheets are kept intact. Rectangles use their original top-left pixel coordinates.
enum BadgeArtwork {
    struct Region:Decodable {let sheet:String;let x,y,width,height:Int}
    static let regions:[String:Region] = {
        guard let url=Bundle.main.url(forResource:"manifest",withExtension:"json",subdirectory:"Badges"),let data=try? Data(contentsOf:url) else{return [:]}
        return (try? JSONDecoder().decode([String:Region].self,from:data)) ?? [:]
    }()
    private static var images:[String:NSImage]=[:]
    static func image(_ key:String)->NSImage? {
        if let cached=images[key]{return cached}
        guard let r=regions[key],let url=Bundle.main.url(forResource:r.sheet,withExtension:"png",subdirectory:"Badges"),let source=CGImageSourceCreateWithURL(url as CFURL,nil),let sheet=CGImageSourceCreateImageAtIndex(source,0,nil),let crop=sheet.cropping(to:CGRect(x:r.x,y:r.y,width:r.width,height:r.height)) else{return nil}
        let image=NSImage(cgImage:crop,size:NSSize(width:r.width,height:r.height));images[key]=image;return image
    }
}
struct TrainerBadgeView:View {
    let badge:TrainerBadge
    let ink:Color
    var compactStar=false
    private var status:String {badge.earned ? "Earned" : badge.recorded ? "Not earned" : "Not recorded"}
    var body:some View {
        Group {
            if compactStar {
                Image(systemName:badge.earned ? "star.fill":"star").font(.system(size:18,weight:.medium))
                    .foregroundStyle(badge.earned ? ink:.white.opacity(0.35)).frame(width:24,height:28)
            } else {
                VStack(spacing:8) {
                    ZStack(alignment:.bottomTrailing) {
                        RoundedRectangle(cornerRadius:14).fill(.white.opacity(badge.earned ? 0.12:0.035))
                            .overlay(RoundedRectangle(cornerRadius:14).stroke(badge.earned ? ink.opacity(0.65):.white.opacity(0.12),lineWidth:1))
                        Group {
                            if let image=BadgeArtwork.image(badge.artwork){Image(nsImage:image).resizable().interpolation(.none).scaledToFit()}
                            else{Image(systemName:badge.symbol).resizable().scaledToFit().foregroundStyle(ink).padding(3)}
                        }.frame(width:32,height:32).frame(maxWidth:.infinity,maxHeight:.infinity)
                            .opacity(badge.earned ? 1:0.32).saturation(badge.earned ? 1:0)
                        Image(systemName:badge.earned ? "checkmark.circle.fill" : badge.recorded ? "circle":"questionmark.circle.fill")
                            .font(.system(size:11,weight:.semibold)).foregroundStyle(badge.earned ? ink:.white.opacity(0.5))
                            .padding(3).background(Color(hex:"142640"),in:Circle()).offset(x:3,y:3)
                    }.frame(width:52,height:52)
                    Text(badge.name.components(separatedBy:" · ").first ?? badge.name)
                        .font(.system(size:11,weight:.medium)).multilineTextAlignment(.center)
                        .foregroundStyle(.white.opacity(badge.earned ? 1:0.6)).lineLimit(2).frame(height:28,alignment:.top)
                }.frame(width:62)
            }
        }.help(badge.name+" · "+status).accessibilityElement(children:.ignore)
            .accessibilityLabel(badge.name).accessibilityValue(status)
    }
}
struct TrainerBadgeStrip:View {
    let badges:[TrainerBadge]
    let ink:Color
    let stars:Bool
    @State private var selected:TrainerBadge?
    private var groups:[String] {badges.reduce(into:[]){if !$0.contains($1.group){$0.append($1.group)}}}
    var body:some View {
        VStack(alignment:.leading,spacing:12) {
            HStack {
                Text(stars ? "SURVEY CORPS RANK":"BADGES & MILESTONES").tracking(1.4)
                Spacer()
                Text("\(badges.filter{$0.earned}.count) / \(badges.count) earned")
            }.font(.system(size:10,weight:.bold)).foregroundStyle(ink)
            ScrollView(.horizontal) {
                HStack(alignment:.top,spacing:20) {
                    ForEach(groups,id:\.self){group in
                        VStack(alignment:.leading,spacing:10) {
                            if !stars {Text(group.capitalized).font(.caption.weight(.semibold)).foregroundStyle(.white.opacity(0.65))}
                            HStack(alignment:.top,spacing:8) {
                                ForEach(badges.filter{$0.group==group}) {badge in
                                    Button{selected=badge}label:{TrainerBadgeView(badge:badge,ink:ink,compactStar:stars)}.buttonStyle(.plain)
                                }
                            }
                        }
                    }
                }.padding(.vertical,5).padding(.trailing,6)
            }.scrollIndicators(.visible)
            Text(stars ? "Select a star to see your recorded rank." : "Select a badge for details. Scroll to see the full collection.")
                .font(.caption2).foregroundStyle(.white.opacity(0.5))
        }.popover(item:$selected){badge in
            VStack(spacing:14) {
                Group {
                    if let image=BadgeArtwork.image(badge.artwork){Image(nsImage:image).resizable().interpolation(.none).scaledToFit()}
                    else{Image(systemName:badge.symbol).resizable().scaledToFit().foregroundStyle(ink)}
                }.frame(width:64,height:64).opacity(badge.earned ? 1:0.4)
                Text(badge.name).font(.headline).multilineTextAlignment(.center)
                Text(badge.group.capitalized).font(.subheadline).foregroundStyle(.secondary)
                Label(badge.earned ? "Earned" : badge.recorded ? "Not earned yet":"Not recorded in this save",systemImage:badge.earned ? "checkmark.seal.fill":badge.recorded ? "circle.dashed":"questionmark.circle")
                    .font(.callout.weight(.medium)).foregroundStyle(badge.earned ? Color.green:Color.secondary)
                Text("Read from your loaded save.").font(.caption).foregroundStyle(.secondary)
            }.padding(24).frame(width:260)
        }
    }
}
