// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

/// <summary>
/// Event arguments used on completion of discovery
/// </summary>
[DataContract]
public class DiscoveryCompleteEventArgs : EventArgs
{
    public DiscoveryCompleteEventArgs() { }

    /// <summary>
    /// Constructor for creating event args object
    /// </summary>
    /// <param name="totalTests">Total tests which got discovered</param>
    /// <param name="isAborted">Specifies if discovery has been aborted.</param>
    public DiscoveryCompleteEventArgs(long totalTests, bool isAborted)
    {
        TotalCount = totalTests;
        IsAborted = isAborted;
    }

    /// <summary>
    ///   Indicates the total tests which got discovered in this request.
    /// </summary>
    [DataMember]
    public long TotalCount { get; set; }

    /// <summary>
    /// Specifies if discovery has been aborted. If true TotalCount is also set to -1.
    /// </summary>
    [DataMember]
    public bool IsAborted { get; set; }

    /// <summary>
    /// Metrics
    /// </summary>
    [DataMember]
    public IDictionary<string, object>? Metrics { get; set; }

    /// <summary>
    /// Gets or sets the list of sources which were fully discovered.
    /// </summary>
    [DataMember]
    public IList<string>? FullyDiscoveredSources { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of sources which were partially discovered (started discover tests, but then discovery aborted).
    /// </summary>
    [DataMember]
    // Added in protocol version 6.
    public IList<string>? PartiallyDiscoveredSources { get; set; } = new List<string>();

    /// <summary>
    ///  Gets or sets the list of sources that were skipped during discovery.
    /// </summary>
    [DataMember]
    // Added in protocol version 7, for previous versions this is put into NotDiscoveredSources.
    public IList<string>? SkippedDiscoveredSources { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of sources which were not discovered at all.
    /// </summary>
    [DataMember]
    public IList<string>? NotDiscoveredSources { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the collection of discovered extensions.
    /// </summary>
    [DataMember]
    public Dictionary<string, HashSet<string>>? DiscoveredExtensions { get; set; } = new();

    /// <summary>
    /// Gets or sets the algorithm that computed the ids of the tests discovered in each source,
    /// keyed by source path, with values such as <c>SHA1</c> or <c>xxHash128</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the algorithm actually used, resolved from the environment, the feature flag and the
    /// built-in default, not the value a run declared. It is reported so that a client which caches
    /// discovery results by test id - Visual Studio's Test Explorer keeps such a store - can notice
    /// that the ids it holds are no longer the ids discovery produces, and re-discover instead of
    /// showing every test twice.
    /// </para>
    /// <para>
    /// It describes the ids the platform computes, not ids an adapter assigns itself: an adapter
    /// that sets <see cref="TestCase.Id"/> directly is unaffected by this algorithm, and its sources
    /// are reported all the same. Acting on the value then costs a re-discovery that changed
    /// nothing, which is the harmless direction to err in.
    /// </para>
    /// <para>
    /// Reported per source rather than once for the whole discovery because that is the granularity
    /// at which it can differ. Each source is discovered by one host, but a solution can mix them:
    /// on .NET each test project brings its own testhost through its own package reference, so a
    /// project still on an older one computes ids the way it always did while its neighbour moves to
    /// a new algorithm. One value for the run would have to collapse that disagreement, leaving a
    /// client to either re-discover everything on every run or miss the source that did change.
    /// Keyed per source, only the entries of a source whose algorithm moved need to be dropped.
    /// </para>
    /// <para>
    /// A name rather than a boolean or an enum, so that an algorithm added later is a new name and a
    /// client that does not recognize a name can treat it the same way it treats a source that is
    /// absent: as ids it cannot vouch for. A source is absent when it was discovered by a version of
    /// the test platform that predates this property, and the whole collection is absent when no
    /// discovery was attempted at all.
    /// </para>
    /// </remarks>
    [DataMember]
    // Additive: a peer that predates this property ignores it on the way in and never sets it on
    // the way out, at every negotiated protocol version.
    public IDictionary<string, string>? TestCaseIdAlgorithms { get; set; }
}
