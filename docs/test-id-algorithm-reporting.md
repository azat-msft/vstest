# Proposal: invalidating the Test Explorer test store when the test id algorithm changes

For the Visual Studio Test Explorer team. This describes what vstest now reports at discovery time,
why the obvious alternatives do not work, and what we suggest Test Explorer does with it.

## The problem

vstest is changing how `TestCase.Id` is computed, from SHA1 to xxHash128, over two releases: the
first ships the algorithm with SHA1 still the default, the second flips the default. See
[PR #16378](https://github.com/microsoft/vstest/pull/16378).

Test Explorer keeps a persisted discovery cache — the test store — at

```
<solution>\.vs\<SolutionName>\v18\TestStore\0\NNN.testlog
<solution>\.vs\<SolutionName>\v18\TestStore\0\testlog.manifest
```

It survives closing Visual Studio, which is the point of it: tests appear without re-discovery. It
also appears to be keyed by `TestCase.Id`.

When the algorithm changes, discovery returns new ids for the same tests. The entries under the old
ids are still in the store, nothing removes them, and **every affected test shows up twice in Test
Explorer**. A user who never touched a feature flag would hit this simply by taking the release that
flips the default.

We cannot fix this from vstest alone. What vstest can do — and now does — is make the algorithm it
used *discoverable*, so Test Explorer can stamp it on the store and drop the store when it changes.

## What vstest now reports

`DiscoveryCompleteEventArgs.TestCaseIdAlgorithm`, a `string?`, on the discovery-completed event the
TranslationLayer already hands you:

```csharp
public void HandleDiscoveryComplete(DiscoveryCompleteEventArgs args, IEnumerable<TestCase>? lastChunk)
{
    string? algorithm = args.TestCaseIdAlgorithm; // "SHA1", "xxHash128", or null
}
```

It carries one of:

| Value | Meaning |
|---|---|
| `"SHA1"` | The ids in this discovery were computed with the legacy SHA1 algorithm. |
| `"xxHash128"` | The ids in this discovery were computed with xxHash128. |
| `null` | Unknown — see below. |

Three properties of this value are deliberate:

**It is the resolved algorithm, not the declared one.** vstest resolves it through environment
variable → feature flag → runsettings → built-in default, and reports the outcome. You do not have
to reimplement that precedence, and you do not have to notice when it changes.

**It is a name, not a boolean.** A third algorithm later is simply a third name. Treat any name you
do not recognize the same way you treat `null`.

**It is not telemetry.** It is reported whether or not the user has opted in to telemetry. See
"Why not Metrics" below — this mattered enough to change the design.

`null` means vstest is not vouching for these ids. It happens when the discovery came from a
vstest older than this change, and when the testhosts of one parallel run did not agree (in
practice: one of them was older). It is not an error, but the ids behind it cannot be matched
against a stamp.

Both discovery paths report it: the classic vstest path, where the testhost computes the ids, and
the Microsoft.Testing.Platform path, where the runner does.

## Suggested Test Explorer behaviour

1. Store the reported name alongside the test store, once for the whole store rather than per test.
2. On discovery, compare the reported name with the stored one.
3. If they differ — including when either is `null` or a name you do not recognize — discard the
   store and re-discover once. Do not attempt to merge or migrate: the old ids cannot be mapped to
   the new ones, since the whole point is that the hash changed.
4. If they match, carry on as today.
5. When there is no stamp yet (first run after Test Explorer picks this up), record the reported
   name. Whether you also invalidate once at that point is your call; invalidating is the safer
   choice and costs one discovery.

For a user who never touches the feature flag, this should fire exactly once: on the first discovery
after taking the vstest release that flips the default.

## Why not read the algorithm off the ids

The new ids are RFC 9562 version 8 UUIDs and carry a 4-bit `hashVersion` in the top nibble, while
legacy SHA1 ids are effectively unversioned. So it looks like you could classify a store by
inspecting the ids in it. That does not work, for two independent reasons.

**Mixed-framework solutions are common, and not every adapter routes through vstest's hashing.**
MSTest assigns its own ids: MSTest v4 already emits v8-shaped ids, and MSTest v3 emits unversioned
ones and never will change. A solution with NUnit plus MSTest v3 still contains non-v8 ids after the
switch. "Not all ids look new" therefore cannot distinguish a stale store from a legitimately mixed
one. This is not a hypothetical: GitHub code search finds 182 repositories declaring both xUnit and
NUnit, and 353 declaring both MSTest and xUnit, in a single `Directory.Packages.props`. Concrete
examples include [npgsql/npgsql](https://github.com/npgsql/npgsql) (NUnit for its own tests, xUnit
forced by the third-party `AdoNet.Specification.Tests` conformance suite) and
[open-telemetry/opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet)
(MSTest 4.3.3 alongside xunit 2.9.3, `Verify.Xunit` and `FsCheck.Xunit`).

**The check is not sound per id anyway.** A SHA1 id passes the "looks like v8" test with probability
15/16 × 1/16 × 1/4 ≈ **1.46 %** — measured at 1.464 % over 200 000 samples, matching the analytic
value. Negligible across a whole store, but it means no single id can be classified reliably.

## Why not read the feature flag

Stamping the value of `VSTEST_DISABLE_XXHASH128_TESTCASE_ID` looks equivalent and is not. The
transition that matters most is the **default flipping between releases**, which happens with no
flag set and no user action. A flag-value stamp reads "nothing set" both before and after, while
every id changes underneath. It would appear to work while missing the one case the feature exists
for.

## Why not Metrics

`DiscoveryCompleteEventArgs.Metrics` was the obvious channel and is the wrong one: it is gated on
telemetry opt-in at both ends. In the testhost the metrics collection is a `NoOpMetricsCollection`
when telemetry is off, so the dictionary is empty at the source; and vstest.console skips enriching
the message at all when `RequestData.IsTelemetryOptedIn` is false.

A user who opted out of telemetry would therefore receive no stamp, Test Explorer would not
invalidate, and they would get the duplicate-tests bug — silently, and only for that subset of
users. A correctness signal cannot ride on a telemetry channel, so this is a plain payload field
instead.

## Compatibility

The value travels as a new property on `DiscoveryCompletePayload`. It is additive at every
negotiated protocol version, and needs no version bump:

- **New vstest, old Visual Studio.** The extra JSON property is ignored by both serializers vstest
  uses. Nothing breaks; you simply do not see the value.
- **Old vstest, new Visual Studio.** The property is absent, so `TestCaseIdAlgorithm` is `null` —
  which, per the table above, already means "cannot vouch for these ids".

## Open questions and things we did not verify

- **We did not decode the `.testlog` format.** We observed the magic bytes `!!tItseT` and id GUIDs
  inside a 22 MB file; we did not reverse-engineer the schema, and this proposal does not require it.
- **We infer that the store is keyed on `TestCase.Id`** from the duplicate-tests symptom plus those
  GUIDs, not from Visual Studio source, which we cannot read.
- **The store path we looked at was `v18`.** Other Visual Studio versions may differ.
- **Whether Test Explorer can ship this on a compatible schedule is your call.** The vstest side is
  a prerequisite either way, but on its own it does not fix the bug — this is half of a two-sided
  fix.
