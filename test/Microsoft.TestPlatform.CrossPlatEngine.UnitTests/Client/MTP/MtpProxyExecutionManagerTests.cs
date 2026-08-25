// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Testing.Platform.ServerMode.Client;
using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Client.MTP;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.UnitTests.Client.MTP;

/// <summary>
/// Tests that drive <see cref="MtpProxyExecutionManager"/> against a fake MTP server.
/// </summary>
/// <remarks>
/// Not parallelized: these tests swap the process-wide <see cref="MtpServerClientFactory.Launch"/>
/// seam, so running them alongside another class that does the same would let one class's fake leak
/// into the other's run.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class MtpProxyExecutionManagerTests
{
    private const string Source = @"C:\tests\MtpApp.dll";

    private Func<string, MtpServerClientOptions, IMtpServerClient>? _originalLaunch;
    private FakeMtpServerClient _client = null!;
    private Mock<IInternalTestRunEventsHandler> _eventHandler = null!;

    [TestInitialize]
    public void Initialize()
    {
        _originalLaunch = MtpServerClientFactory.Launch;
        _client = new FakeMtpServerClient();
        MtpServerClientFactory.Launch = (_, _) => _client;
        _eventHandler = new Mock<IInternalTestRunEventsHandler>();
    }

    [TestCleanup]
    public void Cleanup()
        => MtpServerClientFactory.Launch = _originalLaunch!;

    private static TestCase TestCaseWithUid(string uid)
    {
        var testCase = new TestCase("My.Tests.MyTest", new Uri(MtpTestNodeConverter.DefaultExecutorUri), Source);
        testCase.SetPropertyValue(MtpTestNodeConverter.MtpUidProperty, uid);
        return testCase;
    }

    private static TestCase TestCaseWithoutUid()
        => new("My.Tests.MyTest", new Uri(MtpTestNodeConverter.DefaultExecutorUri), Source);

    private static TestRunCriteria CriteriaFor(params TestCase[] tests)
        => new(tests, 1);

    /// <summary>
    /// The server matches a run filter on node uid alone, so the uid stored at discovery is what
    /// must be sent - not the display name or the fully qualified name.
    /// </summary>
    [TestMethod]
    public void StartTestRunSendsTheMtpNodeUidAsTheRunFilter()
    {
        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaFor(TestCaseWithUid("node-uid-1")), _eventHandler.Object);

        Assert.IsNotNull(_client.RunFilterUids);
        Assert.AreEqual("node-uid-1", _client.RunFilterUids.Single());
    }

    /// <summary>
    /// A TestCase with no MTP uid cannot be addressed: the server would match nothing and the run
    /// would report success having executed zero of the selected tests. The manager must surface
    /// that as an error instead of silently running nothing.
    /// </summary>
    [TestMethod]
    public void StartTestRunFailsLoudlyWhenATestCarriesNoMtpUid()
    {
        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaFor(TestCaseWithoutUid()), _eventHandler.Object);

        Assert.IsNull(_client.RunFilterUids, "No run may be requested when the selection cannot be expressed.");
        _eventHandler.Verify(
            h => h.HandleLogMessage(ObjectModel.Logging.TestMessageLevel.Error, It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// The whole source is aborted rather than silently running the addressable subset: reporting a
    /// partial run as if it were the run the user asked for is the same class of bug this fix exists
    /// to remove.
    /// </summary>
    [TestMethod]
    public void StartTestRunFailsLoudlyWhenOnlySomeTestsCarryAnMtpUid()
    {
        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaFor(TestCaseWithUid("node-uid-1"), TestCaseWithoutUid()), _eventHandler.Object);

        Assert.IsNull(
            _client.RunFilterUids,
            "A selection that cannot be fully expressed must not be partially run.");
        _eventHandler.Verify(
            h => h.HandleLogMessage(ObjectModel.Logging.TestMessageLevel.Error, It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public void StartTestRunRunsEveryTestWhenNoSpecificTestsAreSelected()
    {
        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(new TestRunCriteria([Source], 1), _eventHandler.Object);

        Assert.IsNull(_client.RunFilterUids, "An unfiltered run must not send a uid filter at all.");
        Assert.IsTrue(_client.ExitCalled);
    }

    [TestMethod]
    public void StartTestRunAsksTheServerToExitAndDisposesTheClient()
    {
        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaFor(TestCaseWithUid("node-uid-1")), _eventHandler.Object);

        Assert.IsTrue(_client.ExitCalled);
        Assert.IsTrue(_client.Disposed);
    }

    /// <summary>
    /// Exit runs in a finally block, so a run that fails part-way through still shuts the test
    /// application down rather than leaking the process.
    /// </summary>
    [TestMethod]
    public void StartTestRunExitsWhenTheRunFails()
    {
        _client.ThrowFromRequest = new InvalidOperationException("server blew up");

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaFor(TestCaseWithUid("node-uid-1")), _eventHandler.Object);

        Assert.IsTrue(_client.ExitCalled);
        Assert.IsTrue(_client.Disposed);
    }

    /// <summary>
    /// Cancelling a run cancels the token the in-flight request is riding on. Exit must not be tied
    /// to that token, or a cancelled run would skip the shutdown handshake entirely.
    /// </summary>
    [TestMethod]
    public void StartTestRunStillExitsWhenTheRunIsCancelled()
    {
        _client.ThrowFromRequest = new OperationCanceledException();

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaFor(TestCaseWithUid("node-uid-1")), _eventHandler.Object);

        Assert.IsTrue(_client.ExitCalled, "A cancelled run must still shut the test application down.");
        Assert.IsFalse(
            _client.ExitToken.IsCancellationRequested,
            "Exit must not be driven by the cancelled run token, or it would be skipped.");
        Assert.IsTrue(_client.Disposed, "The launched test application must never outlive a cancelled run.");
    }

    /// <summary>
    /// Builds an MTP node the fake server reports during discovery.
    /// </summary>
    private static MtpTestNodeUpdate DiscoveredNode(string uid, string type, string method)
        => new(
            new Dictionary<string, object?>
            {
                ["uid"] = uid,
                ["display-name"] = method,
                ["node-type"] = "action",
                ["location.type"] = type,
                ["location.method"] = method,
            },
            parentUid: null);

    private static TestRunCriteria CriteriaWithFilter(string filter)
        => new([Source], 1, keepAlive: false, string.Empty, TimeSpan.MaxValue, testHostLauncher: null, filter, filterOptions: null);

    /// <summary>
    /// The headline behaviour: a /TestCaseFilter run must execute only the matching tests. MTP has no
    /// notion of the vstest filter expression, so vstest.console discovers the source, evaluates the
    /// expression itself and runs the matching node uids. Before this the filter was dropped entirely
    /// and the server ran the whole suite while reporting success.
    /// </summary>
    [TestMethod]
    public void StartTestRunRunsOnlyTheTestsMatchingTheTestCaseFilter()
    {
        _client.NodesToPush =
        [
            DiscoveredNode("uid-passes", "My.Tests.UnitTests", "TestPasses"),
            DiscoveredNode("uid-fails", "My.Tests.UnitTests", "TestFails"),
        ];

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaWithFilter("FullyQualifiedName~TestPasses"), _eventHandler.Object);

        Assert.IsNotNull(_client.RunFilterUids, "A filtered run must address the matching tests by uid.");
        Assert.AreEqual("uid-passes", _client.RunFilterUids.Single());
    }

    /// <summary>
    /// "The filter matched nothing" and "no filter was given" must not collapse into the same call.
    /// RunTestsAsync with no uid filter means "run everything" to the server, so a non-matching filter
    /// has to skip the source entirely rather than pass an empty list.
    /// </summary>
    [TestMethod]
    public void StartTestRunRunsNothingWhenTheTestCaseFilterMatchesNoTest()
    {
        _client.NodesToPush = [DiscoveredNode("uid-passes", "My.Tests.UnitTests", "TestPasses")];

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaWithFilter("FullyQualifiedName~NoSuchTest"), _eventHandler.Object);

        Assert.IsFalse(_client.RunRequested, "A filter matching nothing must not start a run at all.");
        _eventHandler.Verify(
            h => h.HandleLogMessage(ObjectModel.Logging.TestMessageLevel.Warning, It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// Filters address traits and the other test case properties, not just the fully qualified name.
    /// </summary>
    [TestMethod]
    public void StartTestRunMatchesTheTestCaseFilterAgainstDisplayName()
    {
        _client.NodesToPush =
        [
            DiscoveredNode("uid-passes", "My.Tests.UnitTests", "TestPasses"),
            DiscoveredNode("uid-fails", "My.Tests.UnitTests", "TestFails"),
        ];

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaWithFilter("DisplayName~TestFails"), _eventHandler.Object);

        Assert.IsNotNull(_client.RunFilterUids);
        Assert.AreEqual("uid-fails", _client.RunFilterUids.Single());
    }

    /// <summary>
    /// An unparseable filter must fail the run rather than degrade into running the whole suite, which
    /// is the same silent-wrong-answer failure mode the filter support exists to remove.
    /// </summary>
    [TestMethod]
    public void StartTestRunFailsLoudlyWhenTheTestCaseFilterCannotBeParsed()
    {
        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaWithFilter("("), _eventHandler.Object);

        Assert.IsFalse(_client.RunRequested, "An unusable filter must never run every test.");
        _eventHandler.Verify(
            h => h.HandleLogMessage(ObjectModel.Logging.TestMessageLevel.Error, It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    /// <summary>
    /// A source whose discovery cannot even be started must not cancel the sources that can be. The
    /// filter is global so an unparseable expression takes the whole run down, but a failure to launch
    /// one application is local to it: the other sources still run, and the run is marked aborted so
    /// the failure is not passed off as success.
    /// </summary>
    [TestMethod]
    public void StartTestRunKeepsRunningOtherSourcesWhenOneSourceCannotBeDiscovered()
    {
        const string goodSource = @"C:\tests\GoodApp.dll";
        const string badSource = @"C:\tests\BadApp.dll";

        var goodClient = new FakeMtpServerClient
        {
            NodesToPush = [DiscoveredNode("uid-passes", "My.Tests.UnitTests", "TestPasses")],
        };

        MtpServerClientFactory.Launch = (source, _) => source == badSource
            ? throw new InvalidOperationException("could not launch")
            : goodClient;

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(
            new TestRunCriteria([badSource, goodSource], 1, keepAlive: false, string.Empty, TimeSpan.MaxValue, testHostLauncher: null, "FullyQualifiedName~TestPasses", filterOptions: null),
            _eventHandler.Object);

        Assert.IsNotNull(goodClient.RunFilterUids, "The healthy source must still run its matching tests.");
        Assert.AreEqual("uid-passes", goodClient.RunFilterUids.Single());
        _eventHandler.Verify(
            h => h.HandleLogMessage(ObjectModel.Logging.TestMessageLevel.Error, It.IsAny<string>()),
            Times.AtLeastOnce);
        _eventHandler.Verify(
            h => h.HandleTestRunComplete(
                It.Is<TestRunCompleteEventArgs>(args => args.IsAborted),
                It.IsAny<TestRunChangedEventArgs>(),
                It.IsAny<ICollection<AttachmentSet>>(),
                It.IsAny<ICollection<string>>()),
            Times.Once,
            "A source that could not be discovered must mark the run aborted.");
    }

    /// <summary>
    /// A mistyped property name matches nothing, exactly like a filter that is merely too narrow. The
    /// run must say which property it did not recognize, or the two are indistinguishable.
    /// </summary>
    [TestMethod]
    public void StartTestRunReportsFilterPropertiesNoDiscoveredTestCarries()
    {
        _client.NodesToPush = [DiscoveredNode("uid-passes", "My.Tests.UnitTests", "TestPasses")];

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaWithFilter("NoSuchProperty=Whatever"), _eventHandler.Object);

        Assert.IsFalse(_client.RunRequested);
        _eventHandler.Verify(
            h => h.HandleLogMessage(
                ObjectModel.Logging.TestMessageLevel.Warning,
                It.Is<string>(message => message.Contains("NoSuchProperty"))),
            Times.AtLeastOnce,
            "The unrecognized property must be named.");
    }

    /// <summary>
    /// A filter naming only real properties that simply match nothing must NOT be reported as naming an
    /// invalid property - that would be a false alarm on a perfectly valid, merely narrow filter.
    /// </summary>
    [TestMethod]
    public void StartTestRunDoesNotReportInvalidPropertiesForAValidButNarrowFilter()
    {
        _client.NodesToPush = [DiscoveredNode("uid-passes", "My.Tests.UnitTests", "TestPasses")];

        using var manager = new MtpProxyExecutionManager();
        manager.StartTestRun(CriteriaWithFilter("FullyQualifiedName~NoSuchTest"), _eventHandler.Object);

        _eventHandler.Verify(
            h => h.HandleLogMessage(
                ObjectModel.Logging.TestMessageLevel.Warning,
                It.Is<string>(message => message.Contains("not valid"))),
            Times.Never,
            "A valid property that matched nothing must not be reported as invalid.");
    }
}
