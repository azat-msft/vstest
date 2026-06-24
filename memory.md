# Efficiency Improver — Repo Memory

## Last Updated
2026-06-24

## Validated Commands

| Action | Command |
|--------|---------|
| Build (Linux, Release) | `./build.sh --build --restore -c Release` |
| Unit tests (specific) | `.dotnet/dotnet test <project.csproj> -c Release -f net11.0` |
| Full unit tests | `./test.sh` |
| Build + pack | `./build.sh --pack -c Release` |

Notes:
- `./test.sh -p <pattern>` does NOT work — dots in project names cause MSBuild errors
- Use `.dotnet/dotnet test <csproj> -f net11.0` for CrossPlatEngine unit tests
- TFMs: `net11.0` and `net481`

## Efficiency Notes

- `TestRunCache.OnNewTestResult` is the per-test hottest path — called once per test result
- `MsTestV1TelemetryHelper.AddTelemetry` is called per MSTestV1 test result from `TestRunCache`
- `DiscoveryDataAggregator.MarkSourcesBasedOnDiscoveredTestCases` — now uses private helper to avoid per-test-case array (fixed 2026-06-24)
- Fork `azat-msft/vstest` is BEHIND `microsoft/vstest` main; need to merge upstream before new branches

## Optimisation Backlog

| Priority | File | Issue | Estimated Impact |
|----------|------|-------|-----------------|
| MEDIUM-HIGH | `CrossPlatEngine/Client/Parallel/ParallelOperationManager.cs:67,104,138,169,204,232,257` | Unguarded `$""` interpolation in scheduler (7 sites) | MEDIUM |
| MEDIUM | `CrossPlatEngine/DataCollection/InProcDataCollector.cs:116` | Uncached `Type.GetMethod()` per in-proc data-collection event | MEDIUM |
| MEDIUM | `CrossPlatEngine/Client/Parallel/ParallelRunDataAggregator.cs:197` | 4-5 `string.Contains` scans per metric key aggregation | MEDIUM |
| LOW-MEDIUM | `CrossPlatEngine/Constants.cs:18` | `ReadOnlyCollection` → `HashSet` for `DefaultAdapters` | LOW-MEDIUM |
| LOW | `CrossPlatEngine/Execution/MSTestV1TelemetryHelper.cs:70-76` | `ContainsKey+indexer` double-lookup → `TryGetValue` (MsTestV1 only) | LOW |
| LOW | `Common/Utilities/AssemblyResolver.cs:150` | `Stack<string>.Contains()` O(n) for re-entrancy guard | LOW |

## Work In Progress / Completed

| PR / Issue | Description | Status |
|-----------|-------------|--------|
| microsoft/vstest#16165 | perf: pre-allocate List(T) capacity in DiscoveryResultCache and TestRunCache | Open draft, all CI green (2026-06-24) |
| #aw_pr_disco (new PR) | perf: eliminate per-test-case array allocation in MarkSourcesBasedOnDiscoveredTestCases | PR submitted 2026-06-24, awaiting number |
| microsoft/vstest#16160 | perf: eliminate closure allocations and redundant dict lookups in FastFilter.Evaluate | MERGED 2026-06-24 |
| microsoft/vstest#16150 | perf: replace ManualResetEvent with ManualResetEventSlim | MERGED |
| microsoft/vstest#16147 | perf: replace Task.FromResult(0) with Task.CompletedTask | MERGED |
| microsoft/vstest#16144 | perf: replace DateTime.Now with DateTime.UtcNow | MERGED |

## Backlog Cursor
Next time: try ParallelOperationManager unguarded string interpolations, or InProcDataCollector method cache.

## Tasks Run (Round-Robin)

| Task | Last Run |
|------|---------|
| Task 1: Discover Commands | 2026-06-22 |
| Task 2: Identify Opportunities | 2026-06-22 |
| Task 3: Implement Improvement | 2026-06-24 (discovery array allocation) |
| Task 4: Maintain PRs | 2026-06-24 (PR #16165 all CI green) |
| Task 7: Monthly Summary | 2026-06-24 |

## Monthly Activity Summary Issue
- Issue #16140 created for 2026-06 (updated 2026-06-24)

## Previously Checked Off by User
(none yet)
