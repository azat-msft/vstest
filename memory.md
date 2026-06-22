# Efficiency Improver — Repo Memory

## Last Updated
2026-06-22

## Validated Commands

| Action | Command |
|--------|---------|
| Build (Linux, Release) | `./build.sh --build --restore -c Release` |
| Unit tests (specific) | `./test.sh --projects /abs/path/to/project.csproj -c Release` |
| Unit tests (all) | `./test.sh` |
| Build + pack | `./build.sh --pack -c Release` |

Notes:
- `./test.sh -p <pattern>` does NOT work — dots in project names cause MSBuild errors
- Always use absolute paths for `--projects`

## Efficiency Notes

- `TestRunCache.OnNewTestResult` is the per-test hottest path — called once per test result
- `MsTestV1TelemetryHelper.AddTelemetry` is called per MSTestV1 test result from `TestRunCache`
- TRX logger `XmlPersistence.ProcessXPathQuery` uses a cache but has double-lookup (minor)
- `JsoniteConvert.cs` reflection (net462 path): uncached `GetProperties`+`GetCustomAttribute` — HIGH impact

## Optimisation Backlog

| Priority | File | Issue | Estimated Impact |
|----------|------|-------|-----------------|
| HIGH | `CrossPlatEngine/DataCollection/InProcDataCollector.cs:116,132` | `Type.GetMethod()` called per test event — no caching | HIGH (per test) |
| HIGH | `CommunicationUtilities/Serialization/JsoniteConvert.cs:80-86,548-564` | Uncached `GetProperties`+`GetCustomAttribute` per IPC serialization | HIGH |
| MEDIUM-HIGH | `CrossPlatEngine/Client/Parallel/ParallelOperationManager.cs:67,104,138,169,204,232,257` | Unguarded `$""` interpolation in scheduler (7 sites) | MEDIUM-HIGH |
| MEDIUM | `CrossPlatEngine/Client/Parallel/ParallelRunDataAggregator.cs:197` | 4-5 `string.Contains` scans per metric key aggregation | MEDIUM |
| MEDIUM | `CrossPlatEngine/Client/ProxyOperationManager.cs:430-432,479-497` | Uncached `GetRuntimeProperties`/`GetCustomAttributes` per host startup | MEDIUM |
| MEDIUM | `CrossPlatEngine/Client/Parallel/ParallelRunDataAggregator.cs:170-176` | `Collection<T>.Contains()` O(n) in aggregation lock | MEDIUM |
| LOW-MEDIUM | `CrossPlatEngine/Client/Parallel/DiscoveryDataAggregator.cs:59-63` | Two-pass `Count(predicate)` over same dictionary | LOW-MEDIUM |
| LOW-MEDIUM | `CrossPlatEngine/Constants.cs:18` | `ReadOnlyCollection` → `HashSet` for `DefaultAdapters` | LOW-MEDIUM |
| LOW | `Common/Utilities/AssemblyResolver.cs:150` | `Stack<string>.Contains()` O(n) for re-entrancy guard | LOW |
| LOW | `Common/ExtensionFramework/TestPluginCache.cs:355,380` | `List<string>.Contains()` for path deduplication | LOW |

## Work In Progress / Completed

| PR / Issue | Description | Status |
|-----------|-------------|--------|
| efficiency/dictionary-double-lookup-telemetry | Replace ContainsKey+indexer double-lookup with TryGetValue in MsTestV1TelemetryHelper | PR created (2026-06-22) |

## Backlog Cursor
Next time: start with InProcDataCollector reflection caching (HIGH impact).

## Tasks Run (Round-Robin)

| Task | Last Run |
|------|---------|
| Task 1: Discover Commands | 2026-06-22 |
| Task 2: Identify Opportunities | 2026-06-22 |
| Task 3: Implement Improvement | 2026-06-22 |
| Task 7: Monthly Summary | 2026-06-22 |

## Monthly Activity Summary Issue
- No existing issue found for 2026-06 — will be created this run.
