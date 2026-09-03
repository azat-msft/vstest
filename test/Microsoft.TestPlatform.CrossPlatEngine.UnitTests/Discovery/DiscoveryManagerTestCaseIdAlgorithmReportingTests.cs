// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.TestPlatform.TestUtilities;
using Microsoft.VisualStudio.TestPlatform.Common.ExtensionFramework;
using Microsoft.VisualStudio.TestPlatform.Common.Telemetry;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Discovery;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace TestPlatform.CrossPlatEngine.UnitTests.Discovery;

/// <summary>
/// Covers the algorithm names <see cref="DiscoveryManager"/> reports on
/// <see cref="DiscoveryCompleteEventArgs.TestCaseIdAlgorithms"/>, which is how a client that caches
/// discovery results by test id learns that the ids it holds are no longer the ids discovery
/// produces.
/// </summary>
/// <remarks>
/// Not parallelized: these tests mutate the process-wide feature flag and its cached value, and
/// restore both afterwards.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class DiscoveryManagerTestCaseIdAlgorithmReportingTests
{
    private const string FeatureFlagName = "VSTEST_DISABLE_XXHASH128_TESTCASE_ID";

    private const string OptIn = "0";
    private const string OptOut = "1";

    // The reported names, written out rather than read from the product, because a client persists
    // them: if these change, every stored stamp stops matching and this test is the warning.
    private const string Sha1Name = "SHA1";
    private const string XxHash128Name = "xxHash128";

    // The one source these discoveries run against, and therefore the one key in the reported map.
    private static string TestSource => typeof(DiscoveryManagerTestCaseIdAlgorithmReportingTests).Assembly.Location;

    [TestCleanup]
    public void TestCleanup()
    {
        TestDiscoveryExtensionManager.Destroy();
        TestPluginCache.Instance = null;
    }

    [TestMethod]
    public void DiscoveryReportsXxHash128WhenTheRunOptsIn()
        => Assert.AreEqual(XxHash128Name, ReportedAlgorithmWithFlag(OptIn));

    [TestMethod]
    public void DiscoveryReportsSha1WhenTheRunOptsOut()
        => Assert.AreEqual(Sha1Name, ReportedAlgorithmWithFlag(OptOut));

    /// <summary>
    /// Every value other than the opt-in one selects SHA1, exactly as <c>FeatureFlag</c> reads every
    /// other <c>VSTEST_DISABLE_*</c> flag, and the report follows that rather than inventing a
    /// notion of an unrecognized value for itself.
    /// </summary>
    [TestMethod]
    [DataRow(" 1 ")]
    [DataRow("true")]
    [DataRow("nonsense")]
    public void DiscoveryReportsSha1ForEveryValueOtherThanTheOptInOne(string value)
        => Assert.AreEqual(Sha1Name, ReportedAlgorithmWithFlag(value));

    /// <summary>
    /// The value is trimmed before it is read, so a padded opt-in still opts in - and is still
    /// reported as having done so.
    /// </summary>
    [TestMethod]
    public void DiscoveryReportsTheAlgorithmATrimmedValueSelects()
        => Assert.AreEqual(XxHash128Name, ReportedAlgorithmWithFlag(" 0 "));

    /// <summary>
    /// A run that declares nothing is reported too, with the algorithm the default resolves to.
    /// </summary>
    /// <remarks>
    /// This is the case the whole feature exists for. The transition that hurts a client's cache
    /// most is the default flipping between releases, which happens with no flag set and no user
    /// action - so a run that says nothing is exactly the run whose algorithm has to be reported.
    /// The value itself is pinned by <see cref="TheDefaultIsCurrentlyReportedAsSha1"/>; this asserts
    /// only that something is reported at all.
    /// </remarks>
    [TestMethod]
    public void DiscoveryReportsTheDefaultWhenTheRunDeclaresNothing()
        => Assert.IsNotNull(ReportedAlgorithmWithFlag(null));

    /// <summary>
    /// A packaged app reports every test case under its package, so the keys name the package too.
    /// </summary>
    /// <remarks>
    /// The invariant the whole feature rests on is that a key names what a client sees as
    /// <see cref="TestCase.Source"/>. Two things rewrite that between discovery and the client, and
    /// each has its own test: the package substitution here, and the deployment path conversion in
    /// TestRequestHandlerTestCaseIdAlgorithmPathTests. Asserting the invariant end to end against a
    /// discovered test case is not possible here, because the mock adapter the discovery tests use
    /// fabricates its own source rather than reporting the one it was given.
    /// </remarks>
    [TestMethod]
    public void ReportedAlgorithmIsKeyedByThePackageWhenTheRunHasOne()
    {
        const string package = @"C:\apps\Contoso.appx";

        DiscoveryCompleteEventArgs? args = null;
        var handler = new Mock<ITestDiscoveryEventsHandler2>();
        handler
            .Setup(h => h.HandleDiscoveryComplete(It.IsAny<DiscoveryCompleteEventArgs>(), It.IsAny<IEnumerable<TestCase>>()))
            .Callback((DiscoveryCompleteEventArgs complete, IEnumerable<TestCase>? _) => args = complete);

        RunWithFlag(null, () =>
        {
            TestPluginCacheHelper.SetupMockExtensions([typeof(DiscovererEnumeratorTests).Assembly.Location], () => { });

            var criteria = new DiscoveryCriteria([TestSource], 1, null) { Package = package };

            new DiscoveryManager(NonTelemetryRequestData()).DiscoverTests(criteria, handler.Object);
        });

        Assert.IsNotNull(args);
        Assert.IsNotNull(args.TestCaseIdAlgorithms);
        Assert.IsTrue(
            args.TestCaseIdAlgorithms.ContainsKey(package),
            $"Expected the package as the key, got: {string.Join(", ", args.TestCaseIdAlgorithms.Keys)}");
        Assert.IsFalse(
            args.TestCaseIdAlgorithms.ContainsKey(TestSource),
            "The inner source is not what a client sees on the test cases, so it must not be a key.");
    }

    private static IRequestData NonTelemetryRequestData()
    {
        var requestData = new Mock<IRequestData>();
        requestData.Setup(rd => rd.MetricsCollection).Returns(new NoOpMetricsCollection());
        return requestData.Object;
    }

    /// <summary>
    /// Which algorithm that default currently is, asserted on its own.
    /// </summary>
    /// <remarks>
    /// Pinned deliberately: the release that flips the default has to change this line, and changing
    /// it is the moment to check that the reported name flips with the ids rather than lagging them.
    /// </remarks>
    [TestMethod]
    public void TheDefaultIsCurrentlyReportedAsSha1()
        => Assert.AreEqual(Sha1Name, ReportedAlgorithmWithFlag(null));

    /// <summary>
    /// The report survives a user who has opted out of telemetry.
    /// </summary>
    /// <remarks>
    /// The reason this property exists rather than a metric. <c>Metrics</c> is empty at the source
    /// for such a user - the testhost's collection is a <see cref="NoOpMetricsCollection"/> - and is
    /// dropped again on the way through vstest.console. A client that cached ids under the old
    /// algorithm would then never be told to re-discover, and only users with telemetry off would
    /// see every test twice.
    /// </remarks>
    [TestMethod]
    public void DiscoveryReportsTheAlgorithmWhenTelemetryIsOptedOut()
    {
        var requestData = new Mock<IRequestData>();
        requestData.Setup(rd => rd.MetricsCollection).Returns(new NoOpMetricsCollection());
        requestData.Setup(rd => rd.IsTelemetryOptedIn).Returns(false);

        Assert.AreEqual(XxHash128Name, ReportedAlgorithmWithFlag(OptIn, requestData.Object));
    }

    /// <summary>
    /// Aborting before discovery started reports nothing, because there is no source whose ids
    /// could be described.
    /// </summary>
    [TestMethod]
    public void AbortBeforeDiscoveryStartedReportsNoAlgorithms()
    {
        DiscoveryCompleteEventArgs? args = null;
        var handler = new Mock<ITestDiscoveryEventsHandler2>();
        handler
            .Setup(h => h.HandleDiscoveryComplete(It.IsAny<DiscoveryCompleteEventArgs>(), It.IsAny<IEnumerable<TestCase>>()))
            .Callback((DiscoveryCompleteEventArgs complete, IEnumerable<TestCase>? _) => args = complete);

        RunWithFlag(OptIn, () =>
        {
            var requestData = new Mock<IRequestData>();
            requestData.Setup(rd => rd.MetricsCollection).Returns(new NoOpMetricsCollection());

            new DiscoveryManager(requestData.Object).Abort(handler.Object);
        });

        Assert.IsNotNull(args);
        Assert.IsNull(args.TestCaseIdAlgorithms);
    }

    /// <summary>
    /// Runs a discovery with the feature flag set to <paramref name="value"/> and returns the
    /// algorithm name it reported for the one source it discovered.
    /// </summary>
    private static string? ReportedAlgorithmWithFlag(string? value, IRequestData? requestData = null)
    {
        if (requestData is null)
        {
            var mock = new Mock<IRequestData>();
            mock.Setup(rd => rd.MetricsCollection).Returns(new MetricsCollection());
            requestData = mock.Object;
        }

        DiscoveryCompleteEventArgs? args = null;
        var handler = new Mock<ITestDiscoveryEventsHandler2>();
        handler
            .Setup(h => h.HandleDiscoveryComplete(It.IsAny<DiscoveryCompleteEventArgs>(), It.IsAny<IEnumerable<TestCase>>()))
            .Callback((DiscoveryCompleteEventArgs complete, IEnumerable<TestCase>? _) => args = complete);

        RunWithFlag(value, () =>
        {
            TestPluginCacheHelper.SetupMockExtensions(
                [typeof(DiscovererEnumeratorTests).Assembly.Location],
                () => { });

            var criteria = new DiscoveryCriteria([TestSource], 1, null);

            new DiscoveryManager(requestData).DiscoverTests(criteria, handler.Object);
        });

        Assert.IsNotNull(args, "Discovery did not report completion.");
        Assert.IsNotNull(args.TestCaseIdAlgorithms, "Discovery reported no algorithms.");

        // One source, so the map describes exactly it - a second entry would mean the report
        // covered something this discovery never looked at.
        Assert.HasCount(1, args.TestCaseIdAlgorithms);
        return args.TestCaseIdAlgorithms[TestSource];
    }

    private static void RunWithFlag(string? value, Action action)
    {
        string? original = Environment.GetEnvironmentVariable(FeatureFlagName);
        try
        {
            Environment.SetEnvironmentVariable(FeatureFlagName, value);
            ResetFeatureFlagCache();

            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(FeatureFlagName, original);
            ResetFeatureFlagCache();
        }
    }

#pragma warning disable CS0618 // ResetFeatureFlagCacheForTesting is what its name says it is.
    private static void ResetFeatureFlagCache() => TestCase.ResetFeatureFlagCacheForTesting();
#pragma warning restore CS0618
}
