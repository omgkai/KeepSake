import Foundation
import CoreImage
var checks=0
for count in [40,300,500,1000] {
    let payload=Data((0..<count).map{UInt8($0%256)})
    let filter=CIFilter(name:"CIQRCodeGenerator")!;filter.setValue(payload,forKey:"inputMessage");filter.setValue("M",forKey:"inputCorrectionLevel")
    let output=filter.outputImage!
    let padded=output.composited(over:CIImage(color:.white).cropped(to:output.extent.insetBy(dx:-4,dy:-4)))
    let scaled=padded.transformed(by:CGAffineTransform(scaleX:5,y:5))
    let context=CIContext(options:[.useSoftwareRenderer:true])
    guard let cg=context.createCGImage(scaled,from:scaled.extent) else{fatalError("render failed")}
    let image=CIImage(cgImage:cg)
    let detector=CIDetector(ofType:CIDetectorTypeQRCode,context:CIContext(options:[.useSoftwareRenderer:true]),options:[CIDetectorAccuracy:CIDetectorAccuracyHigh])!
    let codes=detector.features(in:image).compactMap{$0 as? CIQRCodeFeature}
    guard codes.count==1,let descriptor=codes[0].symbolDescriptor else{fatalError("QR detection failed for \(count)")}
    guard try PokemonQRCodec.payload(descriptor)==payload else{fatalError("Binary round trip failed")};checks+=1
}
for payload in [Data(),Data([0xFF]),Data([0x40,0x50])] {
    do{_ = try PokemonQRCodec.payload(payload,version:1);fatalError("Expected malformed QR rejection")}catch{checks+=1}
}
print("PASS \(checks) native QR binary/image round trips and malformed-payload checks")
