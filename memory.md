# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-08

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
- Monthly summary issue: July 2026 — #16211 (microsoft/vstest), also new #1 (azat-msft/vstest, created 2026-07-08)
- Previous monthly summary: #16140 (microsoft/vstest) — June 2026 — CLOSED 2026-07-02
- Maintainer closed PRs #16213, #16216, #16222 on 2026-07-07 (per-test micro-alloc savings don't meet new HIGH bar)
- Fork (azat-msft/vstest) is typically behind upstream (microsoft/vstest); always create branches from upstream/main via `git fetch --depth=50 upstream main && git checkout -b <branch> upstream/main`
- **Workflow reframed (2026-07-07, PR #16229):** NEW RULES — ≥15-20% improvement required; focus on fixed per-invocation overhead (startup, JIT, IPC handshake, discovery bootstrap); O(n²) always OK; max 1 PR per run; max 2 open PRs; MEDIUM items go to backlog only, never to PRs
- **REPO CHANGE (2026-07-08):** Workflow now runs on azat-msft/vstest fork instead of microsoft/vstest. Monthly summary now on azat-msft/vstest. PRs still target microsoft/vstest.

## Open PRs
- **microsoft/vstest #16210** — perf: eliminate GetRawText() string allocation in STJ deserializer converters (9 converters)
  - Branch: efficiency/eliminate-getrawtext-in-serializers-07cb97344c6c9463
  - Status: Open, CI ALL GREEN (4/4), `🚢 Ship it!` label — awaiting maintainer merge
  - mergeable_state: blocked (likely awaiting explicit review approval)

## Closed PRs (Reference)
- #16213 — CLOSED 2026-07-07 by prior run (before workflow reframe). Per-test micro-alloc: ConcurrentDictionary.AddOrUpdate in DiscoveryDataAggregator. Below new bar.
- #16216 — CLOSED 2026-07-07 by prior run. Duration.ToString/Guid.ToString elimination. Below new bar.
- #16222 — CLOSED 2026-07-07 by prior run. TryGetGuid/TryGetDateTimeOffset in V2 deserializers. Below new bar.

## Merged/Closed PRs (Historical)
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

## Optimisation Backlog (under new HIGH-impact rules)
All prior HIGH items exhausted. Items below new bar (≥15-20% improvement on hot path required):

| Priority | File | Opportunity | Est. Impact | Notes |
|---|---|---|---|---|
| MEDIUM | XmlRunSettingsUtilities.cs:46-51 | `ReaderSettings` property creates new `XmlReaderSettings` on every call (~12 external call sites). Should be `static readonly`. | <1% per call | Not actionable as standalone PR |
| MEDIUM | TestRequestManager.cs:706-715 | `UpdateRunSettingsIfRequired` triple XML parse: creates XmlDocument then calls `GetRunConfigurationNode` + `GetLoggerRunSettings` (each re-parse full XML). Could consolidate via XmlDocument-based overloads | 1-3ms/run | Need ≥30ms to hit 15% bar |
| LOW | ParallelProxyDiscoveryManager.cs:276,308,317 | Eager string.Join in EqtTrace.Verbose calls (not guarded by IsVerboseEnabled) — runs even when logging disabled | <0.1ms/run | Fix only if bundled |
| LOW | AssemblyResolver.cs:52,69 | Eager string.Join in EqtTrace.Info in ctor and AddSearchDirectories | <0.1ms/run | Fix only if bundled |
| LOW | TestPluginCache.cs | `EqtTrace.Verbose` calls with eager string.Join | marginal | Fix only if bundled |
| LOW | InferRunSettingsHelper.cs:407-445 | `GetEnvironmentVariables` parses full RunSettings XML separately from `GetRunConfigurationNode` parse in UpdateRunSettingsIfRequired | marginal | EnvironmentVariables are skipped in RunConfiguration.FromXml |

**Bottom line:** No single item in backlog reaches ≥15% improvement on its own. The real bottleneck is process launch + JIT warmup (100-300ms), which is not in vstest code. Future scans should focus on: (1) O(n²) patterns that emerge as test counts grow, (2) any new IPC round-trips added, (3) blocking calls introduced on startup path.

**Backlog cursor:** Full codebase scan completed 2026-07-08. No new items found in recent commits (#16202, #16228, #16231, #16235, #16236, #16238). Next scan: check testhost startup JIT warmup patterns and data collector initialization sequences more deeply.

## Tasks Last Run
- Task 7 (Monthly summary): 2026-07-08 18:30 UTC — created new issue on azat-msft/vstest
- Task 4 (Maintain PRs): 2026-07-08 18:30 UTC — verified #16210 CI green, no code fix needed
- Task 2 (Identify opportunities): 2026-07-08 18:30 UTC — scanned recent PRs, no new HIGH items found
- Task 5 (Comment on issues): 2026-07-06 18:40 UTC — no new issues or human comments since last visit
- Task 6 (Measurement infrastructure): 2026-07-05 17:38 UTC (GC alloc gap identified)
- Task 3 (Implement improvement): 2026-07-06 17:32 UTC (PR #16222 created — later closed)
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
