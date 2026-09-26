import AppKit
import Foundation
let root=URL(fileURLWithPath:CommandLine.arguments[1]);try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
func color(_ hex:UInt32)->NSColor {NSColor(srgbRed:CGFloat((hex>>16)&255)/255,green:CGFloat((hex>>8)&255)/255,blue:CGFloat(hex&255)/255,alpha:1)}
let navy=color(0x101D36),gold=color(0xDFBE70)
func draw(_ size:Int)->Data {
 let rep=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:size,pixelsHigh:size,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
 NSGraphicsContext.saveGraphicsState();NSGraphicsContext.current=NSGraphicsContext(bitmapImageRep:rep);let ctx=NSGraphicsContext.current!.cgContext;ctx.scaleBy(x:CGFloat(size)/1024,y:CGFloat(size)/1024)
 NSGradient(starting:color(0x243E61),ending:navy)!.draw(in:NSBezierPath(rect:NSRect(x:0,y:0,width:1024,height:1024)),angle:-55)
 // The cover fills the canvas. No white inset or empty icon border.
 color(0x0B1528).setFill();NSBezierPath(rect:NSRect(x:0,y:0,width:122,height:1024)).fill()
 gold.withAlphaComponent(0.7).setFill();NSBezierPath(rect:NSRect(x:120,y:0,width:5,height:1024)).fill()
 let inset=NSBezierPath(roundedRect:NSRect(x:166,y:59,width:800,height:905),xRadius:32,yRadius:32);gold.withAlphaComponent(0.55).setStroke();inset.lineWidth=3;inset.stroke()
 for y in [230,794] {gold.setFill();NSBezierPath(roundedRect:NSRect(x:55,y:y,width:40,height:10),xRadius:5,yRadius:5).fill()}
 let ball=NSBezierPath(ovalIn:NSRect(x:337,y:385,width:438,height:438));gold.setStroke();ball.lineWidth=27;ball.stroke()
 let seam=NSBezierPath();seam.move(to:NSPoint(x:348,y:604));seam.line(to:NSPoint(x:764,y:604));seam.lineWidth=24;seam.stroke()
 navy.setFill();let center=NSBezierPath(ovalIn:NSRect(x:485,y:533,width:142,height:142));center.fill();gold.setStroke();center.lineWidth=22;center.stroke()
 gold.setFill();NSBezierPath(ovalIn:NSRect(x:529,y:577,width:54,height:54)).fill()
 for (x,y,r) in [(820.0,798.0,28.0),(292.0,416.0,17.0),(817.0,375.0,12.0)] {let p=NSBezierPath();p.move(to:NSPoint(x:x,y:y+r));p.line(to:NSPoint(x:x+r*0.35,y:y+r*0.35));p.line(to:NSPoint(x:x+r,y:y));p.line(to:NSPoint(x:x+r*0.35,y:y-r*0.35));p.line(to:NSPoint(x:x,y:y-r));p.line(to:NSPoint(x:x-r*0.35,y:y-r*0.35));p.line(to:NSPoint(x:x-r,y:y));p.line(to:NSPoint(x:x-r*0.35,y:y+r*0.35));p.close();p.fill()}
 let text="KeepSake" as NSString;let attrs:[NSAttributedString.Key:Any]=[.font:NSFont.systemFont(ofSize:68,weight:.medium),.foregroundColor:gold,.kern:4];let width=text.size(withAttributes:attrs).width;text.draw(at:NSPoint(x:556-width/2,y:226),withAttributes:attrs)
 let subtitle="POKÉMON JOURNAL" as NSString;let small:[NSAttributedString.Key:Any]=[.font:NSFont.systemFont(ofSize:18,weight:.semibold),.foregroundColor:gold.withAlphaComponent(0.75),.kern:5];subtitle.draw(at:NSPoint(x:556-subtitle.size(withAttributes:small).width/2,y:178),withAttributes:small)
 color(0xC8A258).setFill();let ribbon=NSBezierPath();ribbon.move(to:NSPoint(x:845,y:1024));ribbon.line(to:NSPoint(x:910,y:1024));ribbon.line(to:NSPoint(x:910,y:866));ribbon.line(to:NSPoint(x:877,y:889));ribbon.line(to:NSPoint(x:845,y:866));ribbon.close();ribbon.fill()
 NSGraphicsContext.restoreGraphicsState();return rep.representation(using:.png,properties:[:])!
}
for size in [16,32,128,256,512] {try draw(size).write(to:root.appendingPathComponent("icon_\(size)x\(size).png"));try draw(size*2).write(to:root.appendingPathComponent("icon_\(size)x\(size)@2x.png"))}
