# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-03

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
| HIGH | DiscoveryDataAggregator.cs | re-evaluate string[1] elimination (PR #16177 closed; need new angle) | ~400 KB per 10K-test discovery run |
| MEDIUM | MsCoverageReferencedPathMaps (MSBuild target) | Add Inputs/Outputs for true incrementality — see issue #15295 | CPU energy per incremental build |
| LOW | MsTestV1TelemetryHelper.cs | ContainsKey + indexer double-hash in AddTelemetry (MSTest v1 only) | LOW |
| LOW | TestObjectBaseConverter.cs | char.ToString() and TimeSpan.ToString() for custom properties (rare path) | LOW |

**Backlog cursor:** HIGH items mostly exhausted; MEDIUM (MSBuild incrementality) is new candidate; LOW items remain

## Tasks Last Run
- Task 4 (Maintain PRs): 2026-07-03 (this run — PR #16210 CI all green)
- Task 5 (Comment on issues): 2026-07-03 (this run — commented on #15295)
- Task 7 (Monthly summary): 2026-07-03 (this run — updated #16211)
- Task 3 (Implement improvement): 2026-07-03 (earlier run — #16210: GetRawText elimination)
- Task 2 (Identify opportunities): 2026-06-29
- Task 1 (Discover commands): 2026-06-19 (stable)
- Task 6 (Measurement infrastructure): 2026-06-28

## Previously Checked Off Items (by maintainer)
None noted.
