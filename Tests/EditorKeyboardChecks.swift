import AppKit
import SwiftUI

@main struct EditorKeyboardChecks {
    @MainActor static func main() throws {
        let p=BoxKeyboardPosition(box:0,slot:29)
        assert(p.moving(delta:1,boxes:32,slots:30,changeBox:false) == .init(box:1,slot:0))
        assert(BoxKeyboardPosition(box:1,slot:0).moving(delta:-1,boxes:32,slots:30,changeBox:false) == p)
        assert(BoxKeyboardPosition(box:0,slot:0).moving(delta:-6,boxes:32,slots:30,changeBox:false) == .init(box:0,slot:0))
        assert(p.moving(delta:1,boxes:32,slots:30,changeBox:true) == .init(box:1,slot:29))
        assert(BoxKeyboardPosition(box:31,slot:29).moving(delta:1,boxes:32,slots:30,changeBox:false) == .init(box:31,slot:29))
        var position=BoxKeyboardPosition(box:0,slot:0)
        for _ in 0..<95 {position=position.moving(delta:1,boxes:32,slots:30,changeBox:false)}
        assert(position == .init(box:3,slot:5))
        var text="", commits=0
        let input=DraftTextField(title:"Draft",text:Binding(get:{text},set:{text=$0})) {commits+=1}
        let coordinator=input.makeCoordinator(), field=NSTextField(), editor=NSTextView()
        field.stringValue="123"
        coordinator.controlTextDidChange(Notification(name:NSControl.textDidChangeNotification,object:field))
        assert(text=="123" && commits==0)
        assert(coordinator.control(field,textView:editor,doCommandBy:#selector(NSResponder.insertTab(_:))))
        assert(coordinator.control(field,textView:editor,doCommandBy:#selector(NSResponder.insertBacktab(_:))))
        assert(commits==0 && text=="123")
        assert(coordinator.control(field,textView:editor,doCommandBy:#selector(NSResponder.insertNewline(_:))))
        assert(commits==1)
        print("PASS: slot/box boundaries and held-key progression; typing and Tab retain drafts; Return commits")
    }
}
