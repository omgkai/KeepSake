import SwiftUI
import AppKit

// Keep Tab navigation local to AppKit; only Return commits a draft to the engine.
struct DraftTextField: NSViewRepresentable {
    @Environment(\.isEnabled) private var isEnabled
    let title: String
    @Binding var text: String
    var monospaced = false
    var trailing = false
    let commit: () -> Void
    func makeCoordinator() -> Coordinator { Coordinator(self) }
    func makeNSView(context: Context) -> NSTextField {
        let field = NSTextField()
        field.isBezeled = true; field.bezelStyle = .roundedBezel
        field.delegate = context.coordinator
        field.setContentHuggingPriority(.defaultLow, for: .horizontal)
        field.setAccessibilityLabel(title)
        return field
    }
    func updateNSView(_ field: NSTextField, context: Context) {
        context.coordinator.parent = self
        field.isEnabled = isEnabled
        field.placeholderString = title
        // Updating an active field's stringValue resets its selection/insertion point.
        if field.currentEditor() == nil && field.stringValue != text { field.stringValue = text }
        field.font = monospaced ? .monospacedSystemFont(ofSize:13,weight:.regular) : .systemFont(ofSize:13)
        field.alignment = trailing ? .right : .left
    }
    final class Coordinator: NSObject, NSTextFieldDelegate {
        var parent: DraftTextField
        init(_ parent: DraftTextField) { self.parent = parent }
        func controlTextDidChange(_ notification: Notification) {
            if let field = notification.object as? NSTextField { parent.text = field.stringValue }
        }
        private func advance(_ control:NSControl,editor:NSTextView,backwards:Bool) {
            guard let window=control.window else{return}
            window.recalculateKeyViewLoop()
            if backwards {window.selectPreviousKeyView(control)} else {window.selectNextKeyView(control)}
            guard window.firstResponder === editor,let root=window.contentView else{return}
            // Hosting views can omit representable fields from AppKit's key loop.
            func inputs(_ view:NSView)->[NSTextField] {
                guard !view.isHiddenOrHasHiddenAncestor else{return []}
                let own=(view as? NSTextField).map { $0.isEditable && $0.isEnabled ? [$0]:[] } ?? []
                return own + view.subviews.flatMap(inputs)
            }
            let fields=inputs(root).sorted {
                let a=$0.convert($0.bounds,to:nil),b=$1.convert($1.bounds,to:nil)
                return abs(a.midY-b.midY)>3 ? a.midY>b.midY:a.minX<b.minX
            }
            guard fields.count>1,let index=fields.firstIndex(where:{$0 === control}) else{return}
            let next=fields[(index+(backwards ? fields.count-1:1))%fields.count]
            next.scrollToVisible(next.bounds);window.makeFirstResponder(next);next.selectText(nil)
        }
        func control(_ control: NSControl, textView: NSTextView, doCommandBy selector: Selector) -> Bool {
            if selector == #selector(NSResponder.insertTab(_:)) {
                advance(control, editor:textView, backwards:false); return true
            }
            if selector == #selector(NSResponder.insertBacktab(_:)) {
                advance(control, editor:textView, backwards:true); return true
            }
            if selector == #selector(NSResponder.insertNewline(_:)) {
                parent.commit(); return true
            }
            return false
        }
    }
}

struct BoxKeyboardPosition: Equatable {
    var box: Int, slot: Int
    func moving(delta:Int, boxes:Int, slots:Int, changeBox:Bool) -> Self {
        guard boxes > 0, slots > 0 else {return self}
        if changeBox {return Self(box:min(max(0,box+delta),boxes-1),slot:min(slot,slots-1))}
        let index=min(max(0,box*slots+slot+delta),boxes*slots-1)
        return Self(box:index/slots,slot:index%slots)
    }
}

// Window-scoped routing avoids depending on the system's full keyboard access setting.
struct BoxKeyboardCapture:NSViewRepresentable {
    @Binding var active:Bool
    let navigate:(Int,Bool,Bool)->Void
    func makeNSView(context:Context)->CaptureView {let view=CaptureView();view.update(active:$active,navigate:navigate);return view}
    func updateNSView(_ view:CaptureView,context:Context) {view.update(active:$active,navigate:navigate)}
    final class CaptureView:NSView {
        private var monitor:Any?
        private var active:Binding<Bool> = .constant(false)
        private var navigate:((Int,Bool,Bool)->Void)?
        private var wasActive=false
        override var acceptsFirstResponder:Bool {true}
        func update(active:Binding<Bool>,navigate:@escaping(Int,Bool,Bool)->Void) {
            self.active=active;self.navigate=navigate
            if active.wrappedValue && !wasActive {window?.makeFirstResponder(self)}
            wasActive=active.wrappedValue
        }
        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if let monitor {NSEvent.removeMonitor(monitor);self.monitor=nil}
            guard window != nil else{return}
            if active.wrappedValue {window?.makeFirstResponder(self)}
            monitor=NSEvent.addLocalMonitorForEvents(matching:[.keyDown,.leftMouseDown,.rightMouseDown]) {[weak self] event in
                guard let self,let window=self.window,(event.window === window || window.isKeyWindow) else{return event}
                if event.type != .keyDown {self.active.wrappedValue=false;return event}
                guard self.active.wrappedValue,window.attachedSheet==nil,NSApp.modalWindow==nil,
                      !(window.firstResponder is NSTextView) else{return event}
                if event.keyCode==48 {self.active.wrappedValue=false;return event}
                guard event.modifierFlags.intersection([.command,.control,.shift]).isEmpty else{return event}
                let horizontal=event.keyCode==123 || event.keyCode==124
                guard horizontal || event.keyCode==125 || event.keyCode==126 else{return event}
                let box=horizontal && event.modifierFlags.contains(.option)
                let direction=(event.keyCode==123 || event.keyCode==126) ? -1:1
                self.navigate?(direction * (horizontal ? 1:6),box,event.isARepeat)
                return nil
            }
        }
        override func keyDown(with event:NSEvent) {
            let horizontal=event.keyCode==123 || event.keyCode==124
            guard active.wrappedValue,window?.attachedSheet==nil,NSApp.modalWindow==nil,
                  event.modifierFlags.intersection([.command,.control,.shift]).isEmpty,
                  horizontal || event.keyCode==125 || event.keyCode==126 else {super.keyDown(with:event);return}
            let direction=(event.keyCode==123 || event.keyCode==126) ? -1:1
            navigate?(direction * (horizontal ? 1:6),horizontal && event.modifierFlags.contains(.option),event.isARepeat)
        }
        deinit {if let monitor {NSEvent.removeMonitor(monitor)}}
        override func hitTest(_ point:NSPoint)->NSView? {nil}
    }
}
