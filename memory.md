# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-10

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
- Maintainer closed PRs #16213, #16216, #16222 on 2026-07-07 (per-test micro-alloc savings don't meet new HIGH bar)
- Fork (azat-msft/vstest) is typically behind upstream (microsoft/vstest); branches must be created from `origin/main` for safeoutputs to work
- **Workflow reframed (2026-07-07, PR #16229):** NEW RULES — ≥15-20% improvement required; focus on fixed per-invocation overhead (startup, JIT, IPC handshake, discovery bootstrap); O(n²) always OK; max 1 PR per run; max 2 open PRs; MEDIUM items go to backlog only, never to PRs

## Open PRs
- **PR #16263** (microsoft/vstest) — Eliminate 5 remaining `GetRawText()` + `StjSafe.Deserialize` allocs in IPC deserializers
  - Files: TestCaseConverter.cs, AttachmentConverters.cs, AfterTestRunEndResultConverter.cs, DiscoveryCriteriaConverter.cs, ExceptionConverter.cs
  - Branch: efficiency/eliminate-getrawtext-remaining (on azat-msft fork)
  - Status: Created 2026-07-10, draft
  - Run: 29113475185

## Closed/Merged PRs (Reference)
- #16210 — MERGED 2026-07-09 by nohwnd. Eliminated GetRawText() string allocation across 9 STJ deserializer converters
- #16213 — CLOSED 2026-07-07. Per-test micro-alloc: ConcurrentDictionary.AddOrUpdate. Below new bar.
- #16216 — CLOSED 2026-07-07. Duration.ToString/Guid.ToString elimination. Below new bar.
- #16222 — CLOSED 2026-07-07. TryGetGuid/TryGetDateTimeOffset in V2 deserializers. Below new bar.
- #16193 — MERGED (v2 write path Guid.ToString → WriteString(Guid) zero-alloc)
- #16182 — MERGED (FilterExpression leaf-node short-circuit)
- #16179 — MERGED (Condition.Evaluate string[1] fast path)
- #16170 — MERGED (IPC ContainsKey→single TryGetValue)
- #16165 — MERGED (pre-allocate List capacity in DiscoveryResultCache/TestRunCache)
- #16160 — MERGED (FastFilter.Evaluate closure/double-lookup elimination)
- #16150 — MERGED (ManualResetEvent → ManualResetEventSlim in JobQueue)
- #16147 — MERGED (Task.FromResult(0) → Task.CompletedTask)
- #16144 — MERGED (DateTime.Now → UtcNow)
- #16139 — CLOSED by maintainer (FastFilter dict lookups — "not worth it")

## Optimisation Backlog (under new HIGH-impact rules)
| Priority | File | Opportunity | Est. Impact | Notes |
|---|---|---|---|---|
| MEDIUM | XmlRunSettingsUtilities.cs:46-51 | `ReaderSettings` property creates new `XmlReaderSettings` on every call. Should be `static readonly`. | <1% per call | Not standalone PR |
| LOW | ParallelProxyDiscoveryManager.cs:276,308,317 | Eager string.Join in EqtTrace.Verbose calls not guarded | <0.1ms/run | Bundle only |
| LOW | AssemblyResolver.cs:52,69 | Eager string.Join in EqtTrace.Info | <0.1ms/run | Bundle only |

**Bottom line:** No single backlog item reaches ≥15% improvement bar. Real bottleneck is process launch + JIT warmup (100-300ms). Future scans: O(n²) patterns at scale, new IPC round-trips, blocking calls on startup path.

**Backlog cursor:** Scanned CrossPlatEngine, CommunicationUtilities, Common, ObjectModel. No new HIGH items found 2026-07-10.

## Tasks Last Run
- Task 7 (Monthly summary): 2026-07-10 18:xx UTC — updated issue #16211, replaced stale content
- Task 3 (Implement improvement): 2026-07-10 18:xx UTC — created PR #16263 (5 remaining GetRawText sites)
- Task 4 (Maintain PRs): 2026-07-09 — no open PRs at time of check
- Task 2 (Identify opportunities): 2026-07-10 — scanned CrossPlatEngine, CommunicationUtilities; found 5 remaining GetRawText sites
- Task 5 (Comment on issues): 2026-07-06 (stable)
- Task 6 (Measurement infrastructure): 2026-07-09 — no new PR created (previous attempt unclear)
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
