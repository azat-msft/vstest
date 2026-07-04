# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-04

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
- Monthly summary issue: July 2026 — #16211 (microsoft/vstest)
- Previous monthly summary: #16140 (microsoft/vstest) — June 2026 — CLOSED 2026-07-02
- Maintainer feedback: "~400KB allocation per 10K tests" is not considered impactful enough on its own. Aim for changes with >1MB impact or clear CPU-cycle savings.
- Fork (azat-msft/vstest) is typically behind upstream (microsoft/vstest); always create branches from upstream/main via `git fetch --depth=50 upstream main && git checkout -b <branch> upstream/main`

## Open PRs
- **#16210** — perf: eliminate GetRawText() string allocation in STJ deserializer converters (9 converters)
  - Branch: efficiency/eliminate-get-raw-text
  - Status: Open draft; CI ALL GREEN (4/4: Windows Release ✅, macOS ✅, Ubuntu ✅, Source-Build ✅) as of 2026-07-03 17:58 UTC
  - Impact: ~1.x MB fewer transient string allocations per 10K-test run (GetRawText() avoided 9 call sites)
- **#16213** — perf: eliminate O(N) redundant ConcurrentDictionary.AddOrUpdate calls in DiscoveryDataAggregator hot path
  - Branch: efficiency/discovery-aggregator-skip-redundant-updates
  - Status: Open draft; CI ALL GREEN (4/4: Windows ✅, macOS ✅, Ubuntu ✅, Source-Build ✅) as of 2026-07-04 17:45 UTC
  - Impact: N ConcurrentDictionary lock acquisitions → 1 per source; N string[1] allocs → 0; affects every `dotnet test` discovery run
- **TBD (branch: efficiency/eliminate-tostring-write-path)** — perf: eliminate ToString() allocations on STJ write path
  - Created: 2026-07-04 17:36 UTC run (run ID 28714233449)
  - Files: TestCaseConverter.cs (Guid), TestObjectBaseConverter.cs (char + TimeSpan), TestResultConverter.cs (TimeSpan), TestResultConverterV2.cs (TimeSpan), TestExecutionContextConverter.cs (TimeSpan)
  - Impact: ~2.4 MB fewer heap allocations per 10K-test run; uses WriteStringValue(Guid) and stackalloc TryFormat("c") for TimeSpan

## Merged/Closed PRs
- #16139 — closed by maintainer (FastFilter dict lookups — "not worth it")
- #16144 — MERGED (DateTime.Now → UtcNow)
- #16147 — MERGED (Task.FromResult(0) → Task.CompletedTask)
- #16150 — MERGED (ManualResetEvent → ManualResetEventSlim in JobQueue)
- #16160 — MERGED (FastFilter.Evaluate closure/double-lookup elimination)
- #16165 — MERGED (pre-allocate List capacity in DiscoveryResultCache/TestRunCache)
- #16170 — MERGED (IPC ContainsKey→single TryGetValue)
- #16177 — CLOSED by maintainer 2026-06-30 (DiscoveryDataAggregator string[1] — benefit too small)
- #16179 — MERGED (Condition.Evaluate string[1] fast path)
- #16182 — MERGED (FilterExpression leaf-node short-circuit)
- #16193 — MERGED 2026-07-01 (v2 write path Guid.ToString → WriteString(Guid) zero-alloc)

## Optimisation Backlog
| Priority | File | Opportunity | Impact |
|---|---|---|---|
| MEDIUM | MsCoverageReferencedPathMaps (MSBuild target) | Add Inputs/Outputs for true incrementality — see issue #15295 | CPU energy per incremental build |
| LOW | GetRawText().Trim('"') (5 sites, else branch) | Rarely executes; marginal gain from eliminating | marginal |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry (MSTest v1 only) | LOW |

**Backlog cursor:** DiscoveryDataAggregator done (#16213); ToString() write-path done (TBD PR); HIGH items exhausted. Next candidates: MEDIUM (MSBuild incrementality issue #15295).

## Tasks Last Run
- Task 4 (Maintain PRs): 2026-07-04 (this run — PR #16213 CI all green 4/4)
- Task 2 (Identify opportunities): 2026-07-04 (this run — ToString() write-path scan)
- Task 3 (Implement improvement): 2026-07-04 (this run — ToString() write-path 5-file fix)
- Task 7 (Monthly summary): 2026-07-04 (this run — updated #16211, added #16213 and new TBD PR)
- Task 5 (Comment on issues): 2026-07-03 (commented on #15295)
- Task 1 (Discover commands): 2026-06-19 (stable)
- Task 6 (Measurement infrastructure): 2026-06-28

## Previously Checked Off Items (by maintainer)
None noted.
