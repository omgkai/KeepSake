import Foundation

@main struct ContextCacheChecks {
    @MainActor static func main() {
        let cache = ContextCache<[String]>()
        let otherWindow = ContextCache<[String]>()
        var requests = 0
        func read(_ key: String) -> [String] {
            if let saved = cache.value(for:key) { return saved }
            requests += 1
            let result = ["response \(requests)"]
            cache.store(result, for:key, generation:cache.generation)
            return result
        }
        let first = read("forms")
        for _ in 0..<100 { precondition(read("forms") == first) }
        precondition(requests == 1, "Revisiting an unchanged page must reuse its result")
        precondition(otherWindow.value(for:"forms") == nil, "Windows must not share cached data")
        _ = read("met")
        precondition(requests == 2)
        let oldGeneration = cache.generation
        cache.invalidate()
        precondition(cache.value(for:"forms") == nil && cache.value(for:"met") == nil)
        cache.store(["late result"], for:"forms", generation:oldGeneration)
        precondition(cache.value(for:"forms") == nil, "A late request must not restore stale data")
        precondition(read("forms") != first && requests == 3)
        print("Context cache: repeated reads, context invalidation, late responses and window isolation passed.")
    }
}
