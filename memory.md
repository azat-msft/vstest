# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-01

## Build/Test Commands (Validated)
- **Build (Debug):** `./build.sh` — downloads pinned SDK to `.dotnet/` if needed
- **Build (Release / CI-like):** `./build.sh -c Release`
- **Test by pattern:** `./test.sh -p <pattern>` (e.g. `CrossPlatEngine`, `CommunicationUtilities`)
- **Note:** SDK not available in this agent environment; CI must validate builds

## Efficiency Notes
- EqtTrace pattern: `if (EqtTrace.IsVerboseEnabled) { EqtTrace.Verbose($"...{var}..."); }` for any interpolated trace calls
- `Assert.Contains(needle, haystack)` — first param is needle (opposite of StringAssert)
- CA1305: always pass `CultureInfo.InvariantCulture` when calling `TryFormat` on TimeSpan/other IFormattable values
- `JsonElement.TryGetGuid()` / `TryGetDateTimeOffset()` available in NETCOREAPP — zero-allocation parse from JSON element
- Monthly summary issue: July 2026 (microsoft/vstest, #aw_july_summary, exact # TBD)
- Previous monthly summary: #16140 (microsoft/vstest) — June 2026 — CLOSED 2026-07-01
- Maintainer feedback: "~400KB allocation per 10K tests" is not considered impactful enough on its own. Aim for changes with >1MB impact or clear CPU-cycle savings.

## Open PRs
- **#16196** — perf: eliminate ToString allocations in v2 serialization hot path (write)
  - Status: Draft, created 2026-06-30. Supersedes #16193 (CA1305 fix included).
  - URL: https://github.com/microsoft/vstest/pull/16196
- **#aw_pr_read** — perf: use TryGetGuid/TryGetDateTimeOffset in v2 deserialization read path
  - Branch: efficiency/v2-read-path-zero-alloc-parse
  - Status: Created 2026-07-01; CI pending
  - Changes: TestCaseConverterV2.Read (TryGetGuid), TestResultConverterV2.Read (TryGetDateTimeOffset for StartTime+EndTime)
  - Impact: ~2.56 MB fewer short-lived string allocations per 10K-result run

## Closed/Superseded PRs (recent)
- **#16193** — superseded by #16196 (CA1305 build error)
- **#16177** — CLOSED by maintainer 2026-06-30 (benefit too small)

## Merged/Closed PRs
- #16139 — closed by maintainer (FastFilter dict lookups — "not worth it")
- #16144 — MERGED (DateTime.Now → UtcNow)
- #16147 — MERGED (Task.FromResult(0) → Task.CompletedTask)
- #16150 — MERGED (ManualResetEvent → ManualResetEventSlim in JobQueue)
- #16160 — MERGED (FastFilter.Evaluate closure/double-lookup elimination)
- #16165 — MERGED (pre-allocate List capacity in DiscoveryResultCache/TestRunCache)
- #16170 — MERGED (IPC ContainsKey→single TryGetValue)
- #16179 — MERGED (Condition.Evaluate string[1] fast path)
- #16182 — MERGED (FilterExpression leaf-node short-circuit)

## Optimisation Backlog
| Priority | File | Opportunity | Impact |
|---|---|---|---|
| HIGH | Measurement | No BenchmarkDotNet benchmarks; no CI regression detection. | HIGH (foundational) |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry (MSTest v1 only) | LOW |
| LOW | TestObjectBaseConverter.cs | char.ToString() and TimeSpan.ToString() for custom properties (rare path) | LOW |

**Backlog cursor:** LOW priority items (MsTestV1TelemetryHelper, TestObjectBaseConverter) — both read-path v2 items are now in-flight as PRs

## Tasks Last Run
- Task 3 (Implement improvement): 2026-07-01 (this run — #aw_pr_read: read path TryGetGuid/TryGetDateTimeOffset)
- Task 4 (Maintain PRs): 2026-07-01 (this run — checked PR #16196 status)
- Task 7 (Monthly summary): 2026-07-01 (this run — closed June #16140, created July 2026)
- Task 2 (Identify opportunities): 2026-06-29
- Task 5 (Comment on issues): 2026-06-27
- Task 1 (Discover commands): 2026-06-19 (stable)
- Task 6 (Measurement infrastructure): 2026-06-28

## Previously Checked Off Items (by maintainer)
None noted.
