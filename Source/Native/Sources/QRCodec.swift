import Foundation
import CoreImage

// Decode QR segments without a Unicode conversion: Gen 7 scanner payloads contain binary bytes.
enum PokemonQRCodec {
    static func payload(_ descriptor:CIQRCodeDescriptor)throws->Data {
        try payload(descriptor.errorCorrectedPayload,version:descriptor.symbolVersion)
    }
    static func payload(_ data:Data,version:Int)throws->Data {
        let bytes=Array(data);var offset=0;var result=Data()
        func read(_ count:Int)throws->Int {
            guard count>=0,offset+count<=bytes.count*8 else{throw failure("The QR payload is truncated.")}
            var value=0;for _ in 0..<count {value=(value<<1)|Int((bytes[offset/8] >> (7-offset%8)) & 1);offset+=1};return value
        }
        guard (1...40).contains(version) else{throw failure("Invalid QR version.")}
        let group=version<10 ? 0:version<27 ? 1:2
        let alphabet=Array("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:".utf8)
        while offset+4<=bytes.count*8 {
            let mode=try read(4);if mode==0{break}
            switch mode {
            case 4:
                let count=try read(group==0 ? 8:16)
                for _ in 0..<count {result.append(UInt8(try read(8)))}
            case 2:
                var count=try read([9,11,13][group])
                while count>=2 {let pair=try read(11);guard pair<2025 else{throw failure("Invalid QR characters.")};result.append(alphabet[pair/45]);result.append(alphabet[pair%45]);count-=2}
                if count==1 {let value=try read(6);guard value<45 else{throw failure("Invalid QR character.")};result.append(alphabet[value])}
            case 1:
                var count=try read([10,12,14][group])
                while count>0 {let digits=min(3,count);let value=try read(digits==3 ? 10:digits==2 ? 7:4);guard value<Int(pow(10.0,Double(digits))) else{throw failure("Invalid QR number.")};result.append(contentsOf:String(format:"%0*d",digits,value).utf8);count-=digits}
            case 7:
                let first=try read(8)
                let assignment:Int
                if first & 128==0 {assignment=first}
                else if first & 192==128 {assignment=((first & 63)<<8)|(try read(8))}
                else if first & 224==192 {assignment=((first & 31)<<16)|(try read(16))}
                else {throw failure("Invalid QR encoding.")}
                guard assignment==3 || assignment==26 else{throw failure("This QR character encoding is not supported.")}
            default:throw failure("This QR segment type is not a supported Pokémon payload.")
            }
            guard result.count<=16000 else{throw failure("The QR payload is too large.")}
        }
        guard !result.isEmpty else{throw failure("The QR payload is empty.")};return result
    }
    static func failure(_ message:String)->NSError {NSError(domain:"KeepSake.QR",code:1,userInfo:[NSLocalizedDescriptionKey:message])}
}
