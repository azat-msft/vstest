# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-06-27

## Build/Test Commands (Validated)
- **Build (Debug):** `./build.sh` — downloads pinned SDK to `.dotnet/` if needed
- **Build (Release / CI-like):** `./build.sh -c Release`
- **Test by pattern:** `./test.sh -p <pattern>` (e.g. `CrossPlatEngine`, `CommunicationUtilities`)
- **Note:** SDK not available in this agent environment; CI must validate builds

## Efficiency Notes
- EqtTrace pattern: `if (EqtTrace.IsVerboseEnabled) { EqtTrace.Verbose($"...{var}..."); }` for any interpolated trace calls
- `$"..."` with no holes should be `"..."` (no allocation either way, but cleaner)
- `Assert.Contains(needle, haystack)` — first param is needle (opposite of StringAssert)
- PRs created by safe-outputs always have head in microsoft/vstest (not fork), so `push_to_pull_request_branch` won't work for those — use comments with exact diff instead
- PR numbers are not available until after workflow completes (deferred creation)
- Monthly summary issue: #16140 (microsoft/vstest) — June 2026

## Open PRs
- **#16170** — perf: eliminate redundant ContainsKey in IPC deserialization hot path
  - Status: Open (not draft), CI all green, has `🚢 Ship it!` label
  - URL: https://github.com/microsoft/vstest/pull/16170
- **#16177** — perf: eliminate string[1] allocation per test case in discovery source tracking
  - Status: Draft, CI all green, reviewer raised _isMessageSent mid-batch guard concern
  - Efficiency Improver commented 2026-06-27 with exact fix to apply
  - URL: https://github.com/microsoft/vstest/pull/16177
- **efficiency/guard-unguarded-trace-interpolations** — guard EqtTrace.Verbose interpolations in ParallelOperationManager
  - Status: PR submitted this run (2026-06-27), number pending
  - Branch: efficiency/guard-unguarded-trace-interpolations

## Merged/Closed PRs
- #16139 — closed by maintainer (FastFilter dict lookups — "not worth it")
- #16144 — MERGED (DateTime.Now → UtcNow)
- #16147 — MERGED (Task.FromResult(0) → Task.CompletedTask)
- #16150 — MERGED (ManualResetEvent → ManualResetEventSlim in JobQueue)
- #16160 — MERGED (FastFilter.Evaluate closure/double-lookup elimination)
- #16165 — MERGED 2026-06-26 (pre-allocate List capacity in DiscoveryResultCache/TestRunCache)

## Optimisation Backlog
| Priority | File | Opportunity | Impact |
|---|---|---|---|
| LOW-MEDIUM | ParallelRunDataAggregator.cs | 4 string.Contains scans per metric key in aggregation loop (~line 197) | LOW-MED |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry | LOW |
| LOW | Condition.cs | GetPropertyValue: string[1] per non-array property in slow-filter | LOW |

**Backlog cursor:** ParallelRunDataAggregator.cs ~line 197

## Tasks Last Run
- Task 3 (Implement improvement): 2026-06-27
- Task 4 (Maintain PRs): 2026-06-27
- Task 7 (Monthly summary): 2026-06-27
- Task 1 (Discover commands): 2026-06-19 (stable)
- Task 2 (Identify opportunities): 2026-06-26
- Task 5 (Comment on issues): not recently run
- Task 6 (Measurement infrastructure): not recently run

## Previously Checked Off Items (by maintainer)
None noted.
