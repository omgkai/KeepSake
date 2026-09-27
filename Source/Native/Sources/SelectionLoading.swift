// Only the latest requested slot may become the editable entity.
@MainActor
func resolveLatestSelection<Target: Equatable, Value>(
    current: () -> Target?,
    load: (Target) async throws -> Value,
    apply: (Value) -> Void
) async rethrows {
    while let requested = current() {
        let result = try await load(requested)
        if current() == requested {
            apply(result)
            return
        }
    }
}
