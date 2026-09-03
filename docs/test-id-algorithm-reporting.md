# Proposal: invalidating the Test Explorer test store when the test id algorithm changes

For the Visual Studio Test Explorer team.

## The problem

vstest is changing how `TestCase.Id` is computed, from SHA1 to xxHash128, over two releases: the
first ships the algorithm with SHA1 still the default, the second flips the default. See
[PR #16378](https://github.com/microsoft/vstest/pull/16378).

Test Explorer keeps a persisted discovery cache — the test store, under `.vs\<Solution>\v18\TestStore\` —
that appears to be keyed by `TestCase.Id`. When the algorithm changes, discovery returns new ids for
the same tests. The entries under the old ids are still in the store, nothing removes them, and
**every affected test shows up twice**. A user who never touched a feature flag would hit this
simply by taking the release that flips the default.

vstest cannot fix this alone. What it can do — and now does — is report which algorithm it used, so
Test Explorer can stamp that on the store and drop the store when it changes.

## What vstest reports

`DiscoveryCompleteEventArgs.TestCaseIdAlgorithms`, an `IDictionary<string, string>?` keyed by source
path, on the discovery-completed event the TranslationLayer already hands you. Each value is one of:

| Value | Meaning |
|---|---|
| `"SHA1"` | Ids for that source that vstest computed were computed with the legacy SHA1 algorithm. |
| `"xxHash128"` | Ids for that source that vstest computed were computed with xxHash128. |
| source absent | Read it as `"SHA1"` - see below. |

Four things are worth knowing about it:

- **It is the algorithm actually used**, resolved from environment variable, feature flag,
  runsettings and the built-in default. You do not need to reimplement that precedence or track
  changes to it.
- **It describes the ids vstest computes**, not ids an adapter assigns itself. MSTest, for example,
  sets `TestCase.Id` directly, so for an MSTest source the reported name describes an algorithm that
  did not produce those ids. Harmless either way: those ids do not move when vstest's algorithm
  moves, so a spurious invalidation costs one re-discovery and a missed one changes nothing.
- **It is reported per source, not per run.** That is the granularity at which it can genuinely
  differ - see below.
- **It is a name, not a boolean.** A third algorithm later is simply a third name. A name you do not
  recognize is the one case you genuinely cannot interpret: treat it as a mismatch.
- **It does not depend on telemetry opt-in.** This is a correctness signal, so it is reported for
  every user.

### An absent source means SHA1

A source is absent when it was discovered by a vstest older than this change, and the whole property
is absent when the whole discovery was. Read both as `"SHA1"` rather than as "unknown", because a
vstest that does not report the algorithm also does not contain xxHash128 - SHA1 is the only thing it
could have produced.

This matters more than it sounds, and reading it as "unknown" instead is wrong in three ways:

- **The first upgrade would invalidate every store for nothing.** The release that adds the algorithm
  keeps SHA1 as the default and changes no id at all. Comparing an unstamped store against a report
  of `"SHA1"` matches, and nothing happens - which is the intent of shipping it over two releases.
  Treating absent as "unknown" would instead drop every user's store once, on the one release
  designed to be a no-op.
- **A permanently mixed solution would never settle.** The project on the older test SDK reports
  nothing on every run, forever. Read as SHA1 it matches its own stamp and is left alone; read as
  unknown it would be re-discovered on every single discovery.
- **The remaining risk is one wasted re-discovery.** If a user opted in to xxHash128 early, via the
  feature flag, on a Visual Studio that did not yet stamp, the store holds xxHash ids that this rule
  assumes are SHA1. The stamp then disagrees with the next report, so you invalidate once. The guess
  is wrong and the outcome is still safe.

Note that a source is reported even when discovery of it aborted or found nothing: the name states
which algorithm that host would have used, not that the source yielded tests.

Both discovery paths report it: the classic vstest path and the Microsoft.Testing.Platform path.

## Why per source

A single value for the whole discovery would not work, because one solution can legitimately use
several algorithms at once.

On .NET, each test project brings its own testhost through its own `Microsoft.NET.Test.Sdk`
reference, and vstest launches one testhost per project. A solution where one project is pinned to
an older test SDK therefore runs a mix: that project keeps computing ids the way it always did while
its neighbours move to the new algorithm. Reduced to one value, such a run could only report
"unknown", leaving you two bad options - re-discover the whole solution on every single run, or
ignore the unknown and miss the projects whose ids really did change.

Keyed per source, that solution is unremarkable: the stale project reports its own algorithm (or is
absent), the others report theirs, and only the sources that actually changed need re-discovering.

## The message on the wire

The value is a new property on the payload of the existing `TestDiscovery.Completed` message. The
two fields that matter here are the per-test `Id`, which is what the store is keyed by, and
`TestCaseIdAlgorithms`, which says how the ids of each source were computed:

```jsonc
{
  "Version": 7,
  "MessageType": "TestDiscovery.Completed",
  "Payload": {
    "TotalTests": 150,
    "LastDiscoveredTests": [
      {
        "Id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",  // the test id, computed with the algorithm below
        "FullyQualifiedName": "Contoso.Math.Tests.CalculatorTests.SubtractTest",
        "DisplayName": "SubtractTest",
        "ExecutorUri": "executor://MSTestAdapter/v2",
        "Source": "Contoso.Math.Tests.dll",                // the key into the map below
        "CodeFilePath": null,
        "LineNumber": -1,
        "Properties": []
      }
    ],
    "IsAborted": false,
    "Metrics": { "TotalTestsDiscovered": 150 },
    "FullyDiscoveredSources": [],
    "PartiallyDiscoveredSources": [],
    "NotDiscoveredSources": [],
    "SkippedDiscoverySources": [],
    "DiscoveredExtensions": {},

    // NEW. Values are "SHA1" or "xxHash128"; a source discovered by a vstest that predates this is
    // simply absent, as is the whole property, and both mean SHA1.
    "TestCaseIdAlgorithms": {
      "Contoso.Math.Tests.dll": "SHA1"
    }
  }
}
```

Everything except `TestCaseIdAlgorithms` is unchanged. Note that `LastDiscoveredTests` carries only
the final batch - earlier tests arrive in preceding `TestDiscovery.TestFound` messages, which have
no algorithm field. The single map in `TestDiscovery.Completed` covers every test of every source it
names.

## Suggested Test Explorer behaviour

The discovery that reports the algorithm has already delivered the new ids - the `TestFound` batches
precede `TestDiscovery.Completed`. So the action on a mismatch is to *replace* what the store held
for that source, not to run a second discovery.

1. Stamp each source in the store with its reported name, reading an absent source as `"SHA1"`.
2. On discovery, compare each source's reported name with the stored one.
3. If a source's name differs - or is a name you do not recognize - drop what the store held for that
   source and keep only what this discovery just returned. Do not try to merge or migrate: old ids
   cannot be mapped to new ones, since the point is that the hash changed.
4. Leave sources whose names match alone, and leave sources this discovery did not cover alone.
5. A store with no stamps at all is a store written before this existed, so its ids are SHA1. Stamp
   it as such rather than invalidating it.

For a user who never touches the feature flag this costs nothing on the release that adds the
algorithm, and fires exactly once per source on the release that flips the default.

One caveat on ordering: between the first `TestFound` batch and `TestDiscovery.Completed` you know
the new ids but not yet the algorithm. If you merge batches into the live model as they arrive, a
user could briefly see both old and new entries during that one discovery, resolved as soon as the
completion arrives.

## Why not read the algorithm off the ids

The new ids are RFC 9562 version 8 UUIDs and carry a 4-bit `hashVersion` in the top nibble, while
legacy SHA1 ids are effectively unversioned — so it looks like a store could be classified by
inspecting the ids in it. That does not work, for two independent reasons.

**Not every adapter routes through vstest's hashing, and mixed-framework solutions are common.**
MSTest assigns its own ids: MSTest v4 already emits v8-shaped ids, and MSTest v3 emits unversioned
ones and never will change. A solution with NUnit plus MSTest v3 still contains non-v8 ids after the
switch, so "not all ids look new" cannot distinguish a stale store from a legitimately mixed one.
This is not hypothetical: GitHub code search finds 182 repositories declaring both xUnit and NUnit,
and 353 declaring both MSTest and xUnit, in a single `Directory.Packages.props`. Verified examples
include [npgsql/npgsql](https://github.com/npgsql/npgsql) (NUnit for its own tests, xUnit forced by
the third-party `AdoNet.Specification.Tests` conformance suite) and
[open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
(MSTest 4.3.3 alongside xunit 2.9.3).

**The check is not sound per id anyway.** A SHA1 id passes the "looks like v8" test with probability
15/16 × 1/16 × 1/4 ≈ **1.46 %** — measured at 1.464 % over 200 000 samples, matching the analytic
value. Negligible across a whole store, but no single id can be classified reliably.

## Why not read the feature flag

Stamping the value of `VSTEST_DISABLE_XXHASH128_TESTCASE_ID` looks equivalent and is not. The
transition that matters most is the default flipping between releases, which happens with no flag
set and no user action. A flag-value stamp reads "nothing set" both before and after, while every id
changes underneath — it would appear to work while missing the one case the feature exists for.

## Compatibility

The value travels as an additive property on the discovery-complete payload, at every negotiated
protocol version, with no version bump:

- **New vstest, old Visual Studio.** The extra field is ignored. Nothing breaks; you do not see the
  value.
- **Old vstest, new Visual Studio.** The field is absent, so every source is unknown — which already
  means "cannot vouch for these ids".

## Open questions and things we did not verify

- **We did not decode the `.testlog` format.** We observed the magic bytes `!!tItseT` and id GUIDs
  inside a 22 MB file; we did not reverse-engineer the schema, and this proposal does not need it.
- **We infer that the store is keyed on `TestCase.Id`** from the duplicate-tests symptom plus those
  GUIDs, not from Visual Studio source.
- **The store path we looked at was `v18`.** Other Visual Studio versions may differ.
- **Whether Test Explorer can ship this on a compatible schedule is your call.** The vstest side is
  a prerequisite either way, but on its own it does not fix the bug — this is half of a two-sided
  fix.
