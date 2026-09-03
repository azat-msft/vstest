// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;

using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Interfaces;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

namespace Microsoft.TestPlatform.CrossPlatEngine.UnitTests.EventHandlers;

/// <summary>
/// The source paths keying the reported test id algorithms have to survive a deployment-aware host
/// the same way the discovered test cases do.
/// </summary>
/// <remarks>
/// <para>
/// When a testhost runs against a deployed copy - the UWP scenario, driven by --local-path and
/// --remote-path - it discovers under the remote path but reports test cases whose <c>Source</c> has
/// been converted back to the local one. A client matches the reported algorithms against exactly
/// that <c>Source</c>, so keys left as remote paths would match nothing: every source would read as
/// unaccounted for and the client would re-discover on every single run, silently, and only in that
/// scenario.
/// </para>
/// <para>
/// Not parallelized: the conversion is configured from process-wide environment variables, which
/// these tests set and restore.
/// </para>
/// </remarks>
[TestClass]
[DoNotParallelize]
public class TestRequestHandlerTestCaseIdAlgorithmPathTests
{
    private const string LocalPath = @"C:\local\";
    private const string RemotePath = @"C:\remote\";

    private const string LocalPathVariable = "VSTEST_UWP_DEPLOY_LOCAL_PATH";
    private const string RemotePathVariable = "VSTEST_UWP_DEPLOY_REMOTE_PATH";

    [TestMethod]
    public void DiscoveryCompleteConvertsTheAlgorithmSourceKeysBackToTheClientsPaths()
    {
        DiscoveryCompletePayload? payload = SendDiscoveryComplete(
            new Dictionary<string, string> { [RemotePath + "Tests.dll"] = "xxHash128" },
            deploymentAware: true);

        Assert.IsNotNull(payload);
        Assert.IsNotNull(payload.TestCaseIdAlgorithms);
        Assert.IsTrue(
            payload.TestCaseIdAlgorithms.ContainsKey(LocalPath + "Tests.dll"),
            $"Expected a key under the local path, got: {string.Join(", ", payload.TestCaseIdAlgorithms.Keys)}");
        Assert.AreEqual("xxHash128", payload.TestCaseIdAlgorithms[LocalPath + "Tests.dll"]);
    }

    /// <summary>
    /// Without a deployment there is nothing to convert, and the keys are reported as they were.
    /// </summary>
    [TestMethod]
    public void DiscoveryCompleteLeavesTheAlgorithmSourceKeysAloneWithoutADeployment()
    {
        DiscoveryCompletePayload? payload = SendDiscoveryComplete(
            new Dictionary<string, string> { [LocalPath + "Tests.dll"] = "SHA1" },
            deploymentAware: false);

        Assert.IsNotNull(payload);
        Assert.IsNotNull(payload.TestCaseIdAlgorithms);
        Assert.AreEqual("SHA1", payload.TestCaseIdAlgorithms[LocalPath + "Tests.dll"]);
    }

    [TestMethod]
    public void DiscoveryCompleteReportsNoAlgorithmsWhenThereAreNone()
    {
        DiscoveryCompletePayload? payload = SendDiscoveryComplete(testCaseIdAlgorithms: null, deploymentAware: true);

        Assert.IsNotNull(payload);
        Assert.IsNull(payload.TestCaseIdAlgorithms);
    }

    /// <summary>
    /// Runs a discovery-complete through a request handler and returns the payload it put on the
    /// channel.
    /// </summary>
    private static DiscoveryCompletePayload? SendDiscoveryComplete(
        IDictionary<string, string>? testCaseIdAlgorithms,
        bool deploymentAware)
    {
        string? originalLocal = Environment.GetEnvironmentVariable(LocalPathVariable);
        string? originalRemote = Environment.GetEnvironmentVariable(RemotePathVariable);

        try
        {
            Environment.SetEnvironmentVariable(LocalPathVariable, deploymentAware ? LocalPath : null);
            Environment.SetEnvironmentVariable(RemotePathVariable, deploymentAware ? RemotePath : null);

            var channel = new Mock<ICommunicationChannel>();
            channel.Setup(mc => mc.MessageReceived).Returns(new TrackableEvent<MessageReceivedEventArgs>());

            var client = new Mock<ICommunicationEndPoint>();
            var endpointFactory = new Mock<ICommunicationEndpointFactory>();
            endpointFactory.Setup(f => f.Create(It.IsAny<ConnectionRole>())).Returns(client.Object);

            string? sent = null;
            channel.Setup(c => c.Send(It.IsAny<string>())).Callback((string data) => sent = data);

            using var jobQueue = new JobQueue<Action>(
                action => action?.Invoke(),
                "TestHostOperationQueue",
                500,
                25000000,
                true,
                message => EqtTrace.Error(message));

            using var handler = new DeploymentAwareRequestHandler(
                new TestHostConnectionInfo { Endpoint = IPAddress.Loopback + ":123", Role = ConnectionRole.Client },
                endpointFactory.Object,
                jobQueue);

            handler.InitializeCommunication();
            client.Raise(e => e.Connected += null, new ConnectedEventArgs(channel.Object));

            handler.DiscoveryComplete(
                new DiscoveryCompleteEventArgs(1, false) { TestCaseIdAlgorithms = testCaseIdAlgorithms },
                new List<TestCase>());

            Assert.IsNotNull(sent, "The handler sent nothing on the channel.");

            Message message = JsonDataSerializer.Instance.DeserializeMessage(sent);
            return JsonDataSerializer.Instance.DeserializePayload<DiscoveryCompletePayload>(message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(LocalPathVariable, originalLocal);
            Environment.SetEnvironmentVariable(RemotePathVariable, originalRemote);
        }
    }

    /// <summary>
    /// A request handler with the constructor arguments these tests need. The path conversion is
    /// switched on by the <c>VSTEST_UWP_DEPLOY_*</c> environment variables rather than by this type:
    /// <see cref="TestRequestHandler"/> already implements
    /// <see cref="IDeploymentAwareTestRequestHandler"/> itself, so a plain subclass behaves the same.
    /// </summary>
    private sealed class DeploymentAwareRequestHandler : TestRequestHandler
    {
        public DeploymentAwareRequestHandler(
            TestHostConnectionInfo connectionInfo,
            ICommunicationEndpointFactory endpointFactory,
            JobQueue<Action> jobQueue)
            : base(
                connectionInfo,
                endpointFactory,
                JsonDataSerializer.Instance,
                jobQueue,
                _ => { },
                _ => { })
        {
        }
    }
}
