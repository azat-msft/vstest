// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Testing.Platform.ServerMode.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client.MTP;

/// <summary>
/// Runs a single Microsoft.Testing.Platform (MTP) discovery pass against one source and converts the
/// reported test nodes into vstest <see cref="TestCase"/> instances.
///
/// Shared by <see cref="MtpProxyDiscoveryManager"/>, which forwards the tests to the discovery handler,
/// and by <see cref="MtpProxyExecutionManager"/>, which resolves a <c>/TestCaseFilter</c> against the
/// discovered set because MTP has no notion of the vstest filter expression.
/// </summary>
internal static class MtpSourceDiscoverer
{
    /// <summary>
    /// Discovers the tests in <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The MTP application to discover.</param>
    /// <param name="logHandler">Receives the log messages produced by the MTP application.</param>
    /// <param name="cancellationToken">Cancels the discovery pass.</param>
    /// <remarks>
    /// The application is launched with no extra environment variables: discovery must not be run under
    /// an execution-only data collector's profiler variables, which are injected into the run launch
    /// only.
    /// </remarks>
    public static List<TestCase> Discover(
        string source,
        Action<TestMessageLevel, string?> logHandler,
        CancellationToken cancellationToken)
    {
        var discovered = new List<TestCase>();

        MtpServerClientOptions options = MtpClientOptionsFactory.CreateOptions();
        using IMtpServerClient client = MtpServerClientFactory.Launch(source, options);
        client.LogReceived += (_, e) => logHandler(MtpClientOptionsFactory.MapServerLogLevel(e.Level), e.Message);
        client.TestNodesUpdated += (_, e) =>
        {
            foreach (MtpTestNodeUpdate change in e.Changes)
            {
                if (MtpTestNodeConverter.IsActionNode(change))
                {
                    lock (discovered)
                    {
                        discovered.Add(MtpTestNodeConverter.ToTestCase(change, source));
                    }
                }
            }
        };

        try
        {
            client.InitializeAsync(cancellationToken).GetAwaiter().GetResult();

            // Awaiting the discover request is sufficient: server-to-client messages arrive on a single
            // ordered stream that the client reads sequentially and dispatches synchronously, so every
            // node notification has already been delivered by the time the request completes.
            client.DiscoverTestsAsync(cancellationToken).GetAwaiter().GetResult();
        }
        finally
        {
            MtpServerClientFactory.TryExit(client);
        }

        lock (discovered)
        {
            return discovered.ToList();
        }
    }
}
