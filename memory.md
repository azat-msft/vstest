# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-07-25

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
- Monthly summary issue: created fresh each month in fork; check both fork and upstream
- Upstream (microsoft/vstest) is AHEAD of fork (azat-msft/vstest); fork hasn't been synced since initial checkout
- **Workflow reframed (2026-07-07, PR #16229):** NEW RULES — ≥15-20% improvement required; focus on fixed per-invocation overhead (startup, JIT, IPC handshake, discovery bootstrap); O(n²) always OK; max 1 PR per run; max 2 open PRs; MEDIUM items go to backlog only, never to PRs
- `ConcurrentDictionary.GetOrAdd(key, value)` — value overload pre-computes the value BEFORE the cache lookup; always use the lambda/factory overload `GetOrAdd(key, _ => ...)` when the factory is expensive
- `EqtTrace.Verbose(format, args)` evaluates `params object[]` args BEFORE checking if verbose is enabled; use `if (EqtTrace.IsVerboseEnabled)` guard when args include `string.Join` or similar allocations

## Open PRs
None.

## Closed/Merged PRs (Reference)
- efficiency/fix-metadata-cache-getorAdd — closed (no longer visible in GitHub, outcome unknown)
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
- #16263 — CLOSED (branch gone). 5 remaining GetRawText+StjSafe.Deserialize sites — PR did not proceed

## Optimisation Backlog (under new HIGH-impact rules)
| Priority | File | Opportunity | Est. Impact | Notes |
|---|---|---|---|---|
| MEDIUM | 6 IPC deserializer files | `GetRawText().Trim('"')` — fallback path for non-string JSON property values | marginal | Unclear if hot path in practice |
| LOW | XmlRunSettingsUtilities.cs:46-51 | `ReaderSettings` property creates new `XmlReaderSettings` on every call. Should be `static readonly`. | <1% per call | Public API — mutability risk. 12 callers. |
| LOW | TestPluginCache.cs:90-101,150-151 | `string.Join` in `GetExtensionPaths`/`DiscoverTestExtensions` not guarded by `IsVerboseEnabled` | <0.1ms/run | Bundle with other unguarded trace calls |
| LOW | ParallelOperationManager.cs:276,308,317 | Eager string.Join in EqtTrace.Verbose calls not guarded | <0.1ms/run | Bundle only |
| LOW | AssemblyResolver.cs:52,69 | Eager string.Join in EqtTrace.Info | <0.1ms/run | Bundle only |
| LOW | TestPluginCache.cs:416 | `additionalExtensions.All(extensionsList.Contains)` is O(n×m) but N is tiny (5-20) | negligible | O(n²) technically, but N too small to matter |

**Next scan suggestions:** Look at the IPC handshake message flow for any round-trips that could be eliminated; check if there are unnecessary assembly loads during startup; explore lazy initialization in TestLoggerManager and TestExtensionManager.

## Tasks Last Run
- Task 7 (Monthly summary): 2026-07-25 — new July issue created
- Task 2 (Identify opportunities): 2026-07-25 — no new HIGH found; added TestPluginCache trace call notes
- Task 4 (Maintain PRs): 2026-07-25 — no open PRs
- Task 3 (Implement improvement): 2026-07-18 — PR for MetadataReaderHelper GetOrAdd fix (now closed)
- Task 5 (Comment on issues): 2026-07-06 (stable)
- Task 6 (Measurement infrastructure): 2026-07-09 — no new PR created
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
