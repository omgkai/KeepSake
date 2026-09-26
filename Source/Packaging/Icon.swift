import AppKit
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers
let folder = URL(fileURLWithPath:CommandLine.arguments[1],isDirectory:true)
try FileManager.default.createDirectory(at:folder,withIntermediateDirectories:true)
for (size,name) in [(16,"icon_16x16"),(32,"icon_16x16@2x"),(32,"icon_32x32"),(64,"icon_32x32@2x"),(128,"icon_128x128"),(256,"icon_128x128@2x"),(256,"icon_256x256"),(512,"icon_256x256@2x"),(512,"icon_512x512"),(1024,"icon_512x512@2x")] {
 let s=CGFloat(size), space=CGColorSpaceCreateDeviceRGB()
 let c=CGContext(data:nil,width:size,height:size,bitsPerComponent:8,bytesPerRow:0,space:space,bitmapInfo:CGImageAlphaInfo.premultipliedLast.rawValue)!
 let outer=CGRect(x:s*0.06,y:s*0.06,width:s*0.88,height:s*0.88)
 c.addPath(CGPath(roundedRect:outer,cornerWidth:s*0.20,cornerHeight:s*0.20,transform:nil)); c.clip()
 let colors=[CGColor(red:0.16,green:0.10,blue:0.3,alpha:1),CGColor(red:0.34,green:0.25,blue:0.65,alpha:1)] as CFArray
 c.drawLinearGradient(CGGradient(colorsSpace:space,colors:colors,locations:[0,1])!,start:CGPoint(x:0,y:0),end:CGPoint(x:s,y:s),options:[])
 c.saveGState();c.addEllipse(in:CGRect(x:s*0.22,y:s*0.22,width:s*0.56,height:s*0.56));c.clip()
 let ball=[CGColor(red:0.73,green:0.28,blue:0.96,alpha:1),CGColor(red:0.50,green:0.48,blue:1,alpha:1)] as CFArray
 c.drawLinearGradient(CGGradient(colorsSpace:space,colors:ball,locations:[0,1])!,start:CGPoint(x:s/2,y:s*0.2),end:CGPoint(x:s/2,y:s*0.8),options:[])
 c.setFillColor(CGColor(gray:1,alpha:0.85));c.fill(CGRect(x:0,y:s*0.47,width:s,height:s*0.06));c.restoreGState()
 c.setFillColor(CGColor(gray:1,alpha:1));c.fillEllipse(in:CGRect(x:s*0.40,y:s*0.40,width:s*0.20,height:s*0.20))
 c.setFillColor(CGColor(red:0.43,green:0.33,blue:0.83,alpha:1));c.fillEllipse(in:CGRect(x:s*0.445,y:s*0.445,width:s*0.11,height:s*0.11))
 let dest=CGImageDestinationCreateWithURL(folder.appendingPathComponent(name+".png") as CFURL,UTType.png.identifier as CFString,1,nil)!
 CGImageDestinationAddImage(dest,c.makeImage()!,nil);CGImageDestinationFinalize(dest)
}
