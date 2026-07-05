# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-05

## Build/Test Commands (Validated)
- **Build (Debug):** `./build.sh` — downloads pinned SDK to `.dotnet/` if needed
- **Build (Release / CI-like):** `./build.sh -c Release`
- **Test by pattern:** `./test.sh -p <pattern>` (e.g. `CrossPlatEngine`, `CommunicationUtilities`)
- **Serialization perf tests:** `./test.sh -p CommunicationUtilities` with `--filter TestCategory=Performance`
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
  - Branch: efficiency/eliminate-getrawtext-in-serializers-07cb97344c6c9463
  - Status: Open draft; CI ALL GREEN (4/4) as of 2026-07-03
- **#16213** — perf: eliminate O(N) redundant ConcurrentDictionary.AddOrUpdate calls in DiscoveryDataAggregator hot path
  - Branch: efficiency/discovery-source-tracking-opt-724a5cbd5665dbe1
  - Status: Open draft; CI ALL GREEN (4/4) as of 2026-07-04
- **#16216** — perf: eliminate Duration.ToString() and Guid.ToString() allocations in IPC serializers
  - Status: Open draft; CI Source-Build ✅, Windows Build 🔄 in-progress as of 2026-07-05 17:25 UTC
  - Impact: ~10K heap string allocs eliminated per 10K-test run (Duration V1+V2, Guid V1)

## Open Issues (Efficiency Improver created)
- **#16217 (TBD)** — Add GC.GetTotalAllocatedBytes allocation tracking to serialization perf tests
  - Created: 2026-07-05 17:38 UTC (number estimated, not yet indexed)
  - Type: Task 6 measurement infrastructure proposal
  - Proposal: Add Allocations_* test methods to SerializationPerformanceTests using GC.GetTotalAllocatedBytes(precise: true)

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

## Measurement Infrastructure (Task 6) Findings — 2026-07-05
- `SerializationPerformanceTests.cs`: uses Stopwatch only; wall-clock timing; no allocation tracking
- `PerformanceTests.cs`: fully `[Ignore]`d — timing thresholds were brittle in CI
- `perf.ps1`: Windows-only PowerShell; depends on Benchmark PS module; not CI-integrated
- **Gap**: No GC.GetTotalAllocatedBytes() coverage anywhere; allocation impact of our PRs is inferred, not measured
- **Proposed fix**: Issue ~#16217 — add Allocations_* variants for key serialize/deserialize paths

## Optimisation Backlog
| Priority | File | Opportunity | Impact |
|---|---|---|---|
| MEDIUM | SerializationPerformanceTests.cs | Add GC.GetTotalAllocatedBytes() variants — see issue ~#16217 | Measurement enablement |
| LOW | GetRawText().Trim('"') (5 sites, else branch) | Rarely executes; marginal gain from eliminating | marginal |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry (MSTest v1 only) | LOW |

**Backlog cursor:** HIGH items exhausted; Task 6 infrastructure gap identified; next candidate for Task 3 = LOW (marginal gains only remaining).

## Tasks Last Run
- Task 4 (Maintain PRs): 2026-07-05 17:38 UTC (this run — checked #16216 CI; Windows in-progress)
- Task 6 (Measurement infrastructure): 2026-07-05 17:38 UTC (this run — found GC alloc gap, created issue)
- Task 7 (Monthly summary): 2026-07-05 17:38 UTC (this run — updated #16211 with corrected #16216 ref + this run entry)
- Task 2 (Identify opportunities): 2026-07-05 17:00 UTC (previous run)
- Task 3 (Implement improvement): 2026-07-05 17:00 UTC (previous run — PR #16216)
- Task 5 (Comment on issues): 2026-07-03 (commented on #15295)
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
