# Efficiency Improver Memory — azat-msft/vstest
Last updated: 2026-08-29

## Build/Test Commands (Validated)
- **Build (Debug):** `./build.sh` — downloads pinned SDK to `.dotnet/` if needed
- **Build (Release / CI-like):** `./build.sh -c Release`
- **Test by pattern:** `./test.sh --projects <path-or-glob>`
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
- `LogExtensions()` in TestPluginCache.cs already guarded by `if (!EqtTrace.IsVerboseEnabled) return;`
- TestPluginCache.GetExtensionPaths lines 91/97/101: EqtTrace.Verbose calls with string.Join NOT guarded — LOW priority
- LengthPrefixCommunicationChannel: already well-optimized (buffered writes, BinaryWriter/UTF8)
- TestCaseConverterV2: only 1 GetRawText() remaining (cold path fallback at line 64)
- XmlPersistence.cs:682-683: static Regex.Replace with local pattern string — uses internal cache (15-entry), acceptable
- `XmlRunSettingsUtilities.ReaderSettings` is called ~12× per run; each call creates new XmlReaderSettings. Already in backlog as MEDIUM.
- 2026-08-29 scan: PlatformAssemblyResolver, TestAdapterPathArgumentProcessor, DiscoveryManager, DefaultEngineInvoker, CrossPlatEngine execution — nothing new HIGH.

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
| MEDIUM | TestRequestManager.cs:576-584 | `UpdateRunSettingsIfRequired` parses runsettings XML 3× per run (XmlDocument.Load + GetRunConfigurationNode + GetLoggerRunSettings) — could reuse the already-loaded XmlDocument | ~2-5ms per run |
| MEDIUM | 6 IPC deserializer files | `GetRawText().Trim('"')` — fallback path for non-string JSON property values | marginal | Unclear if hot path in practice |
| MEDIUM | XmlRunSettingsUtilities.cs:46-51 | `ReaderSettings` property creates new `XmlReaderSettings` on every call. Should be `static readonly`. | ~16 small allocs per run | Public API — mutability risk. 12+ callers confirmed. |
| MEDIUM | TestObjectBaseConverter.cs:~78 | `GetRawText().Trim('"')` fallback for non-string property values in test object property bag deserialization | cold path | Only fires for non-string property values |
| MEDIUM | TestProperty.cs:255-276 | `Properties` dictionary uses explicit `lock` — ConcurrentDictionary would avoid lock per TestProperty lookup during deserialization | per-test × 8 properties | At N=1 test, impact is negligible. Scales with N. |
| LOW | TestPluginCache.cs:90-101,150-151 | `string.Join` in `GetExtensionPaths`/`DiscoverTestExtensions` not guarded by `IsVerboseEnabled` | <0.1ms/run | Bundle with other unguarded trace calls |
| LOW | ParallelOperationManager.cs:276,308,317 | Eager string.Join in EqtTrace.Verbose calls not guarded | <0.1ms/run | Bundle only |
| LOW | AssemblyResolver.cs:52,69 | Eager string.Join in EqtTrace.Info | <0.1ms/run | Bundle only |
| LOW | TestPluginCache.cs:416 | `additionalExtensions.All(extensionsList.Contains)` is O(n×m) but N is tiny (5-20) | negligible | O(n²) technically, but N too small to matter |

**Next scan suggestions:** Look at TestHostManager startup (DefaultTestHostManager, DotnetTestHostManager); examine ProxyDiscoveryManager handshake overhead; check for repeated LINQ in hot loops within TestRunCache.

## Tasks Last Run
- Task 7 (Monthly summary): 2026-08-29 — new August 2026 issue created
- Task 2 (Identify opportunities): 2026-08-29 — scanned PlatformAssemblyResolver, TestAdapterPathArgumentProcessor, DiscoveryManager, DefaultEngineInvoker, CrossPlatEngine execution; no new HIGH items
- Task 4 (Maintain PRs): 2026-08-29 — no open PRs confirmed
- Task 3 (Implement improvement): 2026-07-18 — no HIGH items in backlog; skip
- Task 5 (Comment on issues): 2026-07-06 (stable)
- Task 6 (Measurement infrastructure): 2026-07-09 — no new PR created
- Task 1 (Discover commands): 2026-06-19 (stable)

## Previously Checked Off Items (by maintainer)
None noted.
