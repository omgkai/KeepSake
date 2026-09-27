# Responsiveness checks

KeepSake 0.43 caches immutable reflection metadata in the bridge. It continues to read current field values and evaluate legality for each response. Selection and save mutations still use the serial bridge queue.

The SwiftUI sidebar remains available while the engine works. Page requests received during another operation are deferred; the latest page is loaded afterward. Pokémon selection highlights the requested slot immediately and coalesces rapid clicks. Only the final response becomes editable, and a failed selection triggers a state reconciliation. Existing pending-edit confirmation and save-export validation remain in place.

## Repeatable checks

Build two release bridge versions, then run:

```sh
python3 Tests/selection_benchmark.py /path/to/baseline/PKHeXBridge /path/to/current/PKHeXBridge
```

The benchmark uses an isolated synthetic Legends: Arceus workspace, five warm-up selections and 60 timed selections per round. It alternates build order over three rounds and reports median, p95 and response size. Run without compilation or other heavy work. These are bridge round-trip measurements, not rendered UI latency or a claim about every page.

The production selection resolver can be tested independently:

```sh
swiftc -parse-as-library Source/Native/Sources/SelectionLoading.swift Tests/SelectionLoadingChecks.swift -o /tmp/keepsake-selection-checks
/tmp/keepsake-selection-checks
```

This checks coalescing, suppression of intermediate results, cancellation and error propagation. `Tests/editor_growth.py` covers mutable fields and undo across game formats. `Tests/save_export_integrity.py` accepts a valid save supplied locally and checks backup/export byte identity and reopening. Never commit personal saves.

## 0.43 local measurements

Same Apple Silicon machine, 0.42 bridge versus 0.43, identical 120,559-byte responses. No build or packaging task was running during the final benchmark. Other desktop activity was not controlled.

| Round | 0.42 median / p95 | 0.43 median / p95 |
| --- | --- | --- |
| 1 | 62.25 / 171.61 ms | 16.67 / 32.92 ms |
| 2 | 62.80 / 141.03 ms | 10.68 / 17.80 ms |
| 3 | 68.30 / 224.32 ms | 12.46 / 17.20 ms |

The median of the three round medians fell from 62.80 to 12.46 ms (about 80%). This isolates engine selection work; visual responsiveness on other machines still needs user feedback.
