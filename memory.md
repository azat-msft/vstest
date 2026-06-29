# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-06-29

## Build/Test Commands (Validated)
- **Build (Debug):** `./build.sh` — downloads pinned SDK to `.dotnet/` if needed
- **Build (Release / CI-like):** `./build.sh -c Release`
- **Test by pattern:** `./test.sh -p <pattern>` (e.g. `CrossPlatEngine`, `CommunicationUtilities`)
- **Note:** SDK not available in this agent environment; CI must validate builds

## Efficiency Notes
- EqtTrace pattern: `if (EqtTrace.IsVerboseEnabled) { EqtTrace.Verbose($"...{var}..."); }` for any interpolated trace calls
- `Assert.Contains(needle, haystack)` — first param is needle (opposite of StringAssert)
- `push_to_pull_request_branch` DOES NOT WORK for PRs created in this environment (branch not available locally). Use comments with exact diff instead.
- CA1305: always pass `CultureInfo.InvariantCulture` when calling `TryFormat` on TimeSpan/other IFormattable values
- `JsonElement.TryGetGuid()` / `TryGetDateTimeOffset()` available in NETCOREAPP — zero-allocation parse from JSON element
- Monthly summary issue: #16140 (microsoft/vstest) — June 2026

## Open PRs
- **#16177** — perf: eliminate string[1] allocation per test case in discovery source tracking
  - Status: Draft, CI FAILED (AzDO Windows Release cancelled, transient infra). Comment posted asking for re-trigger.
  - URL: https://github.com/microsoft/vstest/pull/16177
- **#16193** — perf: eliminate ToString allocations in v2 serialization hot path (write)
  - Status: Draft, CI FAILED (CA1305: TryFormat needs CultureInfo.InvariantCulture)
  - Comment posted with exact fix on 2026-06-29. Maintainer needs to apply fix.
  - URL: https://github.com/microsoft/vstest/pull/16193
- **#aw_pr_v2read** — perf: use TryGetGuid/TryGetDateTimeOffset in v2 protocol read path
  - Status: Created 2026-06-29 (PR number TBD - deferred creation)
  - Changes: TestCaseConverterV2.Read (Guid), TestResultConverterV2.Read (StartTime, EndTime)

## Merged/Closed PRs
- #16139 — closed by maintainer (FastFilter dict lookups — "not worth it")
- #16144 — MERGED (DateTime.Now → UtcNow)
- #16147 — MERGED (Task.FromResult(0) → Task.CompletedTask)
- #16150 — MERGED (ManualResetEvent → ManualResetEventSlim in JobQueue)
- #16160 — MERGED (FastFilter.Evaluate closure/double-lookup elimination)
- #16165 — MERGED (pre-allocate List capacity in DiscoveryResultCache/TestRunCache)
- #16170 — MERGED (IPC ContainsKey→single TryGetValue)
- #16179 — MERGED 2026-06-29 (Condition.Evaluate string[1] fast path)
- #16182 — MERGED 2026-06-29 (FilterExpression leaf-node short-circuit)

## Optimisation Backlog
| Priority | File | Opportunity | Impact |
|---|---|---|---|
| HIGH | Measurement | No BenchmarkDotNet benchmarks; no CI regression detection. | HIGH (foundational) |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry (MSTest v1 only) | LOW |
| LOW | ParallelRunDataAggregator.cs | 4x string.Contains scans per metric key (~line 197) | LOW |
| LOW | TestObjectBaseConverter.cs | char.ToString() and TimeSpan.ToString() for custom properties (rare path) | LOW |

**Backlog cursor:** TestObjectBaseConverter.cs (already noted, LOW priority; consider next backlog scan)

## Tasks Last Run
- Task 4 (Maintain PRs): 2026-06-29 (this run — #16193 CA1305 fix comment posted; PR #16179, #16182 confirmed merged)
- Task 3 (Implement improvement): 2026-06-29 (this run — #aw_pr_v2read: v2 read-path TryGetGuid/TryGetDateTimeOffset)
- Task 7 (Monthly summary): 2026-06-29 (this run)
- Task 2 (Identify opportunities): 2026-06-29 (this run — scanned v2 read path)
- Task 5 (Comment on issues): 2026-06-27
- Task 1 (Discover commands): 2026-06-19 (stable)
- Task 6 (Measurement infrastructure): 2026-06-28

## Previously Checked Off Items (by maintainer)
None noted.
