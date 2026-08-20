# Phase 16 performance baseline

Date: 2026-08-20  
Runtime: Next.js 16.3.1 development server, Chrome DevTools trace, no CPU/network throttling  
Scenario: `http://127.0.0.1:3000/?benchmark=10000`

## Reproducible harness

The standalone editor accepts an opt-in `benchmark` query parameter. It creates a deterministic,
asset-free, flat project and clamps the request to 10,000 elements. This path is never activated
inside the WPF/WebView2 host. The generator and runtime project validity are unit-tested.

## Baseline results

| Measurement | Result |
| --- | ---: |
| Elements requested | 10,000 |
| Total DOM elements | 10,181 |
| Largest sibling set | 10,000 |
| DOM depth | 13 |
| Full style recalculation | 57 ms / 10,007 affected elements |
| Layout update | 67 ms |
| Forced reflow (unattributed) | 69 ms |
| Cold-reload LCP | 156 ms |
| Cold-reload CLS | 0.00 |
| 60 right-arrow nudges, one per animation frame | 42,503.5 ms |
| Effective interaction throughput | about 1.41 updates/second |

The load metrics are good because the initial shell paints before the full benchmark interaction
becomes expensive. They do not indicate editing responsiveness. The 60-step nudge result is the
relevant failure: the current architecture is far from the 60 fps goal.

## Confirmed bottlenecks

1. Every model mutation deep-clones the full Project.
2. All 10,000 canvas elements remain mounted even when outside the viewport.
3. Each `ElementRenderer` subscribes to the full Project for managed-asset URL resolution.
4. Canvas context changes (selection/drag/breakpoint) invalidate the entire rendered tree.
5. History stores as many as 50 full Project snapshots.
6. Revision sync sends a debounced full Project document to the host.

## Network and accessibility observations

- The development trace loaded one local document, one local stylesheet, and local Next/Turbopack
  scripts only; there were no third-party requests or asset dependency chains. Development HMR
  chunks are not treated as a production bundle finding.
- The accessibility snapshot exposes the palette entries as static text even though they are
  clickable/draggable. Keyboard roles, names, and focus behavior belong in the Phase 17
  accessibility pass; the benchmark color input already has an accessible name.

## Performance budgets for the next sub-phases

- Preserve correctness, revision conflict handling, and undo semantics before claiming speedups.
- Keep mounted canvas nodes proportional to the visible viewport plus overscan, not project size.
- A 60-step, requestAnimationFrame-paced nudge should complete close to 1 second in the same lab
  scenario; report the measured result rather than asserting 60 fps from code inspection.
- Avoid any single style/layout task above one frame (16.7 ms) during interaction where practical.
- Keep the benchmark opt-in, deterministic, bounded, and covered by CI tests.

## Planned optimization order

1. Remove per-element full-Project subscriptions and derive stable render inputs.
2. Add viewport culling/spatial indexing while preserving selected/dragged ancestors.
3. Replace full-tree mutation/history copies with structural sharing or patches.
4. Reduce bridge payloads only after model correctness and conflict recovery remain proven.

## Phase 16b result — stable render inputs

The renderer no longer subscribes once per element to the full Project. Managed asset URLs and
breakpoints are reduced to stable, canonical render inputs; selection, drop-target, and live drag
visuals are synchronized only to the affected DOM elements; unchanged cloned leaves are guarded by
`React.memo`. The canonical asset validation boundary is preserved.

| Measurement | 16a baseline | After 16b |
| --- | ---: | ---: |
| Total DOM elements | 10,181 | 10,181 |
| Full style recalculation | 57 ms | 48 ms |
| Layout update | 67 ms | 66 ms |
| Cold-reload LCP | 156 ms | 148 ms |
| Cold-reload CLS | 0.00 | 0.00 |
| 60 right-arrow nudges | 42,503.5 ms | 18,015.8 ms |
| Effective interaction throughput | 1.41/s | 3.33/s |

This is a 2.36× interaction-throughput improvement, but the retained 10,181-element DOM still
dominates style/layout cost. Phase 16c must reduce the mounted tree before the frame budget can be
approached.

## Phase 16c result — viewport culling and spatial indexes

The canvas now renders the viewport plus 400 screen pixels of overscan. Selected/dragged nodes,
their ancestor path, and complete actively dragged subtrees remain mounted. The render tree uses
structural sharing and never mutates the canonical Project. Cached grid and sorted-axis indexes
serve marquee and smart-guide queries; oversized geometry/query regions fall back to bounded
linear scans instead of creating unbounded grid buckets.

| Measurement | 16a baseline | After 16b | After 16c |
| --- | ---: | ---: | ---: |
| Peak elements in cold trace | 10,181 | 10,181 | 847 |
| Settled total DOM elements | 10,181 | 10,181 | 450 |
| Settled mounted project elements | 10,001 | 10,001 | 281 |
| Largest cold-trace sibling set | 10,000 | 10,000 | 700 |
| Cold-reload LCP | 156 ms | 148 ms | 135 ms |
| Cold-reload CLS | 0.00 | 0.00 | 0.00 |
| 60 right-arrow nudges | 42,503.5 ms | 18,015.8 ms | 3,891.5 ms |
| Effective interaction throughput | 1.41/s | 3.33/s | 15.42/s |

The nudge scenario is now 10.9× faster than baseline. Its remaining ~64.9 ms/update is dominated by
full-Project mutation cloning plus full-snapshot history and synchronization work, addressed in
Phase 16d.

## Phase 16d result — immutable path copies and shared history

Every project mutation now path-copies only affected nodes and ancestors. Unchanged branches,
pages, assets, and metadata retain identity. The 50-step history stores these immutable Project
references directly, so undo/redo no longer deep-clones the full document when recording or
restoring a step. Deep cloning remains only for newly inserted duplicate/component subtrees, where
fresh mutable identity is required.

The host bridge deliberately retains its debounced full-Project revision envelope. Replacing it
with partial patches would require a new cross-language transaction/conflict protocol and would
weaken the proven authoritative recovery path; standalone mode now avoids installing unused host
transport subscriptions.

| Measurement | 16a baseline | After 16b | After 16c | After 16d |
| --- | ---: | ---: | ---: | ---: |
| Settled total DOM elements | 10,181 | 10,181 | 450 | 450 |
| Cold-reload LCP | 156 ms | 148 ms | 135 ms | 142 ms |
| Cold-reload CLS | 0.00 | 0.00 | 0.00 | 0.00 |
| 60 right-arrow nudges | 42,503.5 ms | 18,015.8 ms | 3,891.5 ms | 1,558.4 ms |
| Effective interaction throughput | 1.41/s | 3.33/s | 15.42/s | 38.50/s |

The final development-server result is 27.3× baseline throughput and ~26.0 ms/update. It does not
claim a locked 60 fps at 10k, but the architecture no longer scales DOM/history/copy cost directly
with every mounted project node. Phase 17 should preserve these budgets during UI polish.
