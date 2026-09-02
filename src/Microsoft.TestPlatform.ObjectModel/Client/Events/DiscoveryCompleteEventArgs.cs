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
    /// Gets or sets the name of the algorithm that computed the ids of the tests this discovery
    /// reported, e.g. <c>SHA1</c> or <c>xxHash128</c>.
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
    /// A name rather than a boolean or an enum, so that an algorithm added later is a new name and
    /// a client that does not recognize a name can treat it the same way it treats
    /// <see langword="null"/>: as ids it cannot vouch for.
    /// </para>
    /// <para>
    /// <see langword="null"/> when the discovery was performed by a version of the test platform
    /// that predates this property, and on some abort paths that report no discovery at all.
    /// </para>
    /// </remarks>
    [DataMember]
    // Additive: a peer that predates this property ignores it on the way in and never sets it on
    // the way out, at every negotiated protocol version.
    public string? TestCaseIdAlgorithm { get; set; }
}
