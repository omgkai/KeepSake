// Window-local read results, valid only until the model changes.
@MainActor final class ContextCache<Value> {
    private(set) var generation: UInt64 = 0
    private var values: [String: Value] = [:]

    func value(for key: String) -> Value? { values[key] }

    func store(_ value: Value, for key: String, generation expected: UInt64) {
        // An older asynchronous request must not repopulate a refreshed context.
        guard expected == generation else { return }
        values[key] = value
    }

    func invalidate() {
        generation &+= 1
        values.removeAll(keepingCapacity: true)
    }
}
