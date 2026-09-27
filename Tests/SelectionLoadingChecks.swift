import Foundation

@main struct SelectionLoadingChecks {
    @MainActor static func main() async throws {
        var target: Int? = 0
        var loaded: [Int] = [], applied: [Int] = []
        try await resolveLatestSelection(current: { target }, load: { requested in
            loaded.append(requested)
            try await Task.sleep(nanoseconds: 1_000_000)
            if requested == 0 { target = 1; target = 2; target = 3 }
            return requested
        }, apply: { applied.append($0) })
        precondition(loaded == [0, 3] && applied == [3], "Intermediate results must not be applied")
        target = 4; loaded = []; applied = []
        await resolveLatestSelection(current: { target }, load: { requested in
            loaded.append(requested); target = nil; return requested
        }, apply: { applied.append($0) })
        precondition(applied.isEmpty, "Cancelled selection must not be applied")
        enum Failure: Error { case expected }
        target = 5
        do {
            try await resolveLatestSelection(current: { target }, load: { requested -> Int in
                throw Failure.expected
            }, apply: { applied.append($0) })
            preconditionFailure("Failure must reach the model's recovery handler")
        } catch Failure.expected {}
        precondition(applied.isEmpty)
        print("PASS: rapid selection coalescing, stale-result suppression, cancellation and error propagation")
    }
}
