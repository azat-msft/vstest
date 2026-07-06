# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-06

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
  - Branch: efficiency/eliminate-duration-tostring-in-serializers-1deb46d88fccf54d
  - Status: Open draft; CI ALL GREEN (4/4) as of 2026-07-05
- **#16222** — perf: use JsonElement.TryGetGuid/TryGetDateTimeOffset in V2 IPC deserializers
  - Branch: efficiency/eliminate-getstring-parse-in-v2-converters-bd89d610163d9b9a
  - Status: Open draft; CI ALL GREEN (4/4) as of 2026-07-06 18:29 UTC

## Open Issues (Efficiency Improver created)
- None with pending action (issue #16217 about GC allocation tracking was estimated to be created, but check actual issue list)

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
| LOW | TestRunStatisticsConverter.cs | `TestOutcome.ToString()` in WriteNumber — once per TestRunComplete | negligible |
| LOW | Multiple converters (5 sites) | `GetRawText().Trim('"')` in else branch (non-string JSON tokens) | marginal |
| LOW | TestObjectBaseConverter.cs | `WritePropertyValue` TimeSpan case: `ts.ToString()` for custom TimeSpan properties | marginal |

**Backlog cursor:** HIGH items exhausted; all 4 open PRs cover highest-impact remaining IPC allocs. Only LOW/marginal items left.

## Tasks Last Run
- Task 4 (Maintain PRs): 2026-07-06 18:40 UTC — verified #16210, #16213, #16216, #16222 all CI green
- Task 2 (Identify opportunities): 2026-07-06 18:40 UTC — broad scan, HIGH items exhausted, only LOW remain
- Task 5 (Comment on issues): 2026-07-06 18:40 UTC — no new issues or human comments since last visit
- Task 7 (Monthly summary): 2026-07-06 18:40 UTC — updated #16211 with #16222 reference + this run
- Task 6 (Measurement infrastructure): 2026-07-05 17:38 UTC (GC alloc gap identified, issue created)
- Task 3 (Implement improvement): 2026-07-06 17:32 UTC (PR #16222 created)
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
