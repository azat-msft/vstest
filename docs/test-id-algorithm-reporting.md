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

`DiscoveryCompleteEventArgs.TestCaseIdAlgorithm`, a `string?`, on the discovery-completed event the
TranslationLayer already hands you. It carries one of:

| Value | Meaning |
|---|---|
| `"SHA1"` | The ids in this discovery were computed with the legacy SHA1 algorithm. |
| `"xxHash128"` | The ids in this discovery were computed with xxHash128. |
| `null` | Unknown — vstest is not vouching for these ids. |

Three things are worth knowing about it:

- **It is the algorithm actually used**, resolved from environment variable, feature flag,
  runsettings and the built-in default. You do not need to reimplement that precedence or track
  changes to it.
- **It is a name, not a boolean.** A third algorithm later is simply a third name. Treat a name you
  do not recognize exactly as you treat `null`.
- **It does not depend on telemetry opt-in.** This is a correctness signal, so it is reported for
  every user.

`null` happens when the discovery came from a vstest older than this change, or when the testhosts
of one parallel run did not agree (in practice: one of them was older). It is not an error, but ids
behind it cannot be matched against a stamp.

Both discovery paths report it: the classic vstest path and the Microsoft.Testing.Platform path.

## Suggested Test Explorer behaviour

1. Store the reported name alongside the test store, once for the whole store rather than per test.
2. On discovery, compare the reported name with the stored one.
3. If they differ — including when either is `null` or a name you do not recognize — discard the
   store and re-discover once. Do not try to merge or migrate: the old ids cannot be mapped to the
   new ones, since the point is that the hash changed.
4. If they match, carry on as today.
5. When there is no stamp yet, record the reported name. Whether you also invalidate once at that
   point is your call; invalidating is safer and costs one discovery.

For a user who never touches the feature flag, this should fire exactly once: on the first discovery
after taking the vstest release that flips the default.

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
- **Old vstest, new Visual Studio.** The field is absent, so the value is `null` — which already
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
