# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-06-28

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
- **#16177** — perf: eliminate string[1] allocation per test case in discovery source tracking
  - Status: Draft, CI FAILED (2h timeout on Windows Release — unusual; code confirmed correct by expert reviewer in re-review). Comment posted asking maintainer to re-run CI.
  - URL: https://github.com/microsoft/vstest/pull/16177
- **#16179** — perf: avoid string[1] allocation in Condition.Evaluate for single-string properties
  - Status: Draft, CI all green ✅
  - URL: https://github.com/microsoft/vstest/pull/16179
- **#16182** — perf: short-circuit FilterExpression.Evaluate for leaf nodes (was #aw_pr_fexpr)
  - Status: Draft, CI all green ✅ (all OSes + Source-Build)
  - URL: https://github.com/microsoft/vstest/pull/16182

## Merged/Closed PRs
- #16139 — closed by maintainer (FastFilter dict lookups — "not worth it")
- #16144 — MERGED (DateTime.Now → UtcNow)
- #16147 — MERGED (Task.FromResult(0) → Task.CompletedTask)
- #16150 — MERGED (ManualResetEvent → ManualResetEventSlim in JobQueue)
- #16160 — MERGED (FastFilter.Evaluate closure/double-lookup elimination)
- #16165 — MERGED 2026-06-26 (pre-allocate List capacity in DiscoveryResultCache/TestRunCache)
- #16170 — MERGED (IPC ContainsKey→single TryGetValue)

## Optimisation Backlog
| Priority | File | Opportunity | Impact |
|---|---|---|---|
| HIGH | Measurement | No BenchmarkDotNet benchmarks; no CI regression detection. SerializationPerformanceTests.cs exists (Stopwatch, console-only). Benchmark infrastructure issue created 2026-06-28 (number TBD). | HIGH (foundational) |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry | LOW |
| LOW | ParallelRunDataAggregator.cs | 4x string.Contains scans per metric key (~line 197) — keys are short so impact is minimal | LOW |

**Backlog cursor:** MsTestV1TelemetryHelper.cs

## Tasks Last Run
- Task 4 (Maintain PRs): 2026-06-28 (this run — PR #16177 CI failure investigated, comment posted)
- Task 6 (Measurement infrastructure): 2026-06-28 (this run — assessed gaps, created BenchmarkDotNet proposal issue)
- Task 7 (Monthly summary): 2026-06-28 (this run)
- Task 3 (Implement improvement): 2026-06-27
- Task 5 (Comment on issues): 2026-06-27 (issue #16172)
- Task 1 (Discover commands): 2026-06-19 (stable)
- Task 2 (Identify opportunities): 2026-06-26

## Previously Checked Off Items (by maintainer)
None noted.
