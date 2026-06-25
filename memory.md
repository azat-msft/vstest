# Efficiency Improver — Repo Memory

## Last Updated
2026-06-25

## Validated Commands

| Action | Command |
|--------|---------|
| Bootstrap SDK + build | `./build.sh --build --restore -c Release` |
| Build specific project | `.dotnet/dotnet build <project.csproj> -c Release -f net11.0 --no-restore` |
| Run unit tests (filter) | `.dotnet/dotnet test <project.csproj> -c Release -f net11.0 --no-build --filter <filter>` |
| Full unit tests | `./test.sh` |
| Build + pack | `./build.sh --pack -c Release` |

Notes:
- `./test.sh -p <pattern>` does NOT work — dots in project names cause MSBuild errors
- Use `.dotnet/dotnet test <csproj> -f net11.0` for CrossPlatEngine unit tests
- TFMs: `net11.0` and `net481`
- 8 pre-existing Linux failures in CrossPlatEngine.UnitTests (Windows path `C:\` assertions)

## Efficiency Notes

- `TestRunCache.OnNewTestResult` is the per-test hottest path — called once per test result
- `MsTestV1TelemetryHelper.AddTelemetry` is called per MSTestV1 test result from `TestRunCache`
- `DiscoveryDataAggregator.MarkSourcesBasedOnDiscoveredTestCases` — fixed 2026-06-24
- InProcDataCollector: MethodInfo cache eliminates 20K+ Type.GetMethod calls per covered run (fixed 2026-06-25)
- Fork `azat-msft/vstest` is behind `microsoft/vstest` main; need to merge upstream before new branches

## Optimisation Backlog

| Priority | File | Issue | Estimated Impact |
|----------|------|-------|-----------------|
| MEDIUM | `CrossPlatEngine/Client/Parallel/ParallelOperationManager.cs` | 7 unguarded `$""` interpolations in scheduler (lines 67, 104, 138, 169, 204, 232, 257) | MEDIUM |
| LOW-MEDIUM | `CrossPlatEngine/Client/Parallel/ParallelRunDataAggregator.cs:197` | 4 `string.Contains` scans per metric key aggregation | LOW-MEDIUM |
| LOW | `CrossPlatEngine/Execution/MSTestV1TelemetryHelper.cs:70-76` | `ContainsKey+indexer` double-lookup → `TryGetValue` (MsTestV1 only) | LOW |

## Work In Progress / Completed

| PR / Issue | Description | Status |
|-----------|-------------|--------|
| microsoft/vstest#16165 | perf: pre-allocate List(T) capacity in DiscoveryResultCache and TestRunCache | Open draft, all CI green |
| microsoft/vstest#16170 | perf: eliminate redundant ContainsKey in IPC deserialization hot path | Open draft, all CI green |
| efficiency/inproc-collector-method-cache | perf: cache MethodInfo in InProcDataCollector to avoid per-event reflection | PR submitted 2026-06-25, awaiting number |
| microsoft/vstest#16160 | perf: eliminate closure allocations and redundant dict lookups in FastFilter.Evaluate | MERGED |
| microsoft/vstest#16150 | perf: replace ManualResetEvent with ManualResetEventSlim | MERGED |
| microsoft/vstest#16147 | perf: replace Task.FromResult(0) with Task.CompletedTask | MERGED |
| microsoft/vstest#16144 | perf: replace DateTime.Now with DateTime.UtcNow | MERGED |

## Backlog Cursor
Next time: try ParallelOperationManager unguarded string interpolations (lines 67, 104, 138, 169, 204, 232, 257).

## Tasks Run (Round-Robin)

| Task | Last Run |
|------|---------|
| Task 1: Discover Commands | 2026-06-22 |
| Task 2: Identify Opportunities | 2026-06-22 |
| Task 3: Implement Improvement | 2026-06-25 (InProcDataCollector MethodInfo cache) |
| Task 4: Maintain PRs | 2026-06-25 (#16165 and #16170 both CI-green) |
| Task 7: Monthly Summary | 2026-06-25 |

## Monthly Activity Summary Issue
- Issue #16140 created for 2026-06 (updated 2026-06-25 22:11 UTC)

## Previously Checked Off by User
(none yet)
