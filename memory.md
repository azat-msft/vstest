# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-11

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
- Upstream (microsoft/vstest) is AHEAD of fork (azat-msft/vstest); fork hasn't been synced since initial checkout
- To identify real opportunities, check upstream files via GitHub API (not local checkout)
- **Workflow reframed (2026-07-07, PR #16229):** NEW RULES — ≥15-20% improvement required; focus on fixed per-invocation overhead (startup, JIT, IPC handshake, discovery bootstrap); O(n²) always OK; max 1 PR per run; max 2 open PRs; MEDIUM items go to backlog only, never to PRs

## Open PRs
None. All previous efficiency PRs are closed.

## Closed/Merged PRs (Reference)
- #16263 — CLOSED (branch gone). 5 remaining GetRawText+StjSafe.Deserialize sites — PR did not proceed
- #16210 — MERGED 2026-07-09. Eliminated GetRawText() string allocation across 9 STJ deserializer converters
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
| MEDIUM | 6 IPC deserializer files | `GetRawText().Trim('"')` — fallback path for non-string JSON property values | marginal | Unclear if hot path in practice |
| LOW | XmlRunSettingsUtilities.cs:46-51 | `ReaderSettings` property creates new `XmlReaderSettings` on every call. Should be `static readonly`. | <1% per call | Not standalone PR |
| LOW | ParallelOperationManager.cs:276,308,317 | Eager string.Join in EqtTrace.Verbose calls not guarded | <0.1ms/run | Bundle only |
| LOW | AssemblyResolver.cs:52,69 | Eager string.Join in EqtTrace.Info | <0.1ms/run | Bundle only |

**Current status (2026-07-11):** No HIGH-impact items found. Scanned filter eval path, IPC framing, TestRunCache, DiscoveryResultCache, FastFilter, Condition.Evaluate. All optimizations in those areas already merged upstream. 

**Next scan targets:** TestRequestManager/TestEngine startup path, TestPluginDiscoverer, run settings XML parsing overhead, ProxyOperationManager handshake path. These are fixed per-invocation costs that could matter.

## Tasks Last Run
- Task 7 (Monthly summary): 2026-07-11 17:32 UTC — updated issue #16211
- Task 2 (Identify opportunities): 2026-07-11 — scanned filter, IPC framing, cache paths; no new HIGH items
- Task 4 (Maintain PRs): 2026-07-11 — confirmed no open efficiency PRs
- Task 3 (Implement improvement): 2026-07-10 (PR #16263, did not proceed)
- Task 5 (Comment on issues): 2026-07-06 (stable)
- Task 6 (Measurement infrastructure): 2026-07-09 — no new PR created
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
