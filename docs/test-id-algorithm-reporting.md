# Test id algorithm reporting

For the Visual Studio Test Explorer team.

## The problem

vstest is changing how `TestCase.Id` is computed, SHA1 → xxHash128, over two releases: the first
ships the algorithm with SHA1 still the default, the second flips the default. See
[PR #16378](https://github.com/microsoft/vstest/pull/16378).

Test Explorer's test store (`.vs\<Solution>\v18\TestStore\`) appears to be keyed by `TestCase.Id`.
When the algorithm changes, discovery returns new ids for the same tests, the entries under the old
ids stay, and **every affected test shows up twice**.

vstest cannot fix this alone. It can report which algorithm it used, so Test Explorer can stamp that
on the store and replace the stale entries when it changes.

## What vstest reports

`DiscoveryCompleteEventArgs.TestCaseIdAlgorithms` — an `IDictionary<string, string>?` keyed by
source path, values `"SHA1"` or `"xxHash128"` — on the discovery-completed event the TranslationLayer
already hands you. It is the algorithm actually used, resolved from environment variable, feature
flag, runsettings and the default, so you do not need to reimplement that precedence.

On the wire it is one new property on the existing `TestDiscovery.Completed` payload:

```jsonc
{
  "MessageType": "TestDiscovery.Completed",
  "Payload": {
    "LastDiscoveredTests": [
      {
        "Id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",   // keyed by this
        "Source": "Contoso.Math.Tests.dll",             // the key into the map below
        // ...
      }
    ],
    // ... existing fields unchanged ...

    "TestCaseIdAlgorithms": {                           // NEW
      "Contoso.Math.Tests.dll": "SHA1"
    }
  }
}
```

Reported on both the classic vstest path and the Microsoft.Testing.Platform path.

**Per source, not per run**, because that is where it can differ: on .NET each test project brings
its own testhost through its own `Microsoft.NET.Test.Sdk` reference, so a solution with one project
on an older test SDK genuinely runs a mix of algorithms.

**An absent source means `"SHA1"`.** A vstest old enough not to report the algorithm does not contain
xxHash128 either, so SHA1 is the only thing it could have produced. Reading it as "unknown" instead
would drop every user's store on the release that changes no id, and would re-discover the older
project in a mixed solution on every run, forever.

## Suggested behaviour

The `TestFound` batches arrive *before* `TestDiscovery.Completed`, so by the time you learn the
algorithm you already hold the new ids. A mismatch means replace, not re-discover.

1. Stamp each source in the store with its reported name, reading an absent source as `"SHA1"`.
2. On discovery, compare each source's reported name with the stored one.
3. If it differs — or is a name you do not recognize — drop what the store held for that source and
   keep what this discovery just returned. Old ids cannot be mapped to new ones.
4. Leave matching sources, and sources this discovery did not cover, alone.

For a user who never touches the feature flag: nothing happens on the release that adds the
algorithm, and each source is replaced exactly once on the release that flips the default.

Two caveats. Between the first `TestFound` batch and the completion message you know the new ids but
not yet the algorithm, so merging batches into the live model as they arrive can briefly show both
old and new entries. And for adapters that assign their own ids (MSTest does), the reported name
describes an algorithm that did not produce those ids — harmless, since those ids do not move when
ours does.

## Things we did not verify

- We did not decode the `.testlog` format — we saw the magic bytes `!!tItseT` and id GUIDs, nothing
  more.
- That the store is keyed on `TestCase.Id` is inferred from the duplicate-tests symptom, not from
  Visual Studio source.
- The store path we looked at was `v18`.
- Whether Test Explorer can ship this on a compatible schedule is your call. The vstest side is a
  prerequisite either way, but on its own it does not fix the bug.
