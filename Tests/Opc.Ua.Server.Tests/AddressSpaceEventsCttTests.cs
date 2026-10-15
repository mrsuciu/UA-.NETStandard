/* ========================================================================
 * Copyright (c) 2005-2025 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 *
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Quickstarts.ReferenceServer;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Opc.Ua.Server.Tests
{
    /// <summary>
    /// CTT Address Space Model / Address Space Events / 004: monitoring events of
    /// several items where one item cannot be used to subscribe to events.
    /// </summary>
    /// <remarks>
    /// Expected result of the CTT step: "The CreateMonitoredItems call is Good, with
    /// each item returning Good, except for the item with no Notifier attribute which
    /// returns Bad_AttributeIdInvalid."
    /// OPC 10000-3 8.59 EventNotifierType: bit SubscribeToEvents "Indicates if it can
    /// be used to subscribe to Events"; OPC 10000-3 5.5.1 / 5.6 give the EventNotifier
    /// attribute to Objects and Views only. OPC 10000-4 Table 179: Bad_AttributeIdInvalid
    /// "The attribute is not supported for the specified node". Bad_NotSupported is
    /// not a result code of CreateMonitoredItems (OPC 10000-4 5.13.2.4 Table 65).
    /// </remarks>
    [TestFixture]
    [Category("Server")]
    [SetCulture("en-us")]
    [SetUICulture("en-us")]
    public class AddressSpaceEventsCttTests
    {
        private ServerFixture<ReferenceServer> m_fixture;
        private ReferenceServer m_server;
        private RequestHeader m_requestHeader;
        private SecureChannelContext m_secureChannelContext;

        [OneTimeSetUp]
        public async Task OneTimeSetUpAsync()
        {
            m_fixture = new ServerFixture<ReferenceServer>
            {
                AllNodeManagers = true,
                OperationLimits = true,
                DurableSubscriptionsEnabled = false
            };
            m_server = await m_fixture.StartAsync().ConfigureAwait(false);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDownAsync()
        {
            await m_fixture.StopAsync().ConfigureAwait(false);
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            (m_requestHeader, m_secureChannelContext) = await m_server
                .CreateAndActivateSessionAsync(TestContext.CurrentContext.Test.Name)
                .ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            m_requestHeader.Timestamp = DateTime.UtcNow;
            await m_server.CloseSessionAsync(
                m_secureChannelContext,
                m_requestHeader,
                true,
                CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Step 1: CreateMonitoredItems with event items on notifier Objects (not the
        /// Server Object) and on nodes that cannot be used to subscribe to events.
        /// </summary>
        [Test]
        public async Task AddressSpaceEvents004_ItemWithoutEventNotifierReturnsBadAttributeIdInvalidAsync()
        {
            ushort ns = (ushort)m_server.CurrentInstance.NamespaceUris.GetIndex(
                Quickstarts.ReferenceServer.Namespaces.ReferenceServer);
            var notifierFolder = new NodeId("CTT", ns);
            var folderWithoutNotifier = new NodeId("Scalar", ns);
            var variable = new NodeId("Scalar_Static_Boolean", ns);
            NodeId coreVariable = VariableIds.Server_ServerStatus_State;

            // preconditions: the folders exist with and without the SubscribeToEvents bit.
            byte notifier = await ReadEventNotifierAsync(notifierFolder).ConfigureAwait(false);
            Assert.AreEqual(EventNotifiers.SubscribeToEvents, (byte)(notifier & EventNotifiers.SubscribeToEvents));
            Assert.AreEqual(
                EventNotifiers.None,
                await ReadEventNotifierAsync(folderWithoutNotifier).ConfigureAwait(false));

            m_requestHeader.Timestamp = DateTime.UtcNow;
            CreateSubscriptionResponse subscription = await m_server.CreateSubscriptionAsync(
                m_secureChannelContext,
                m_requestHeader,
                1000,
                100,
                10,
                0,
                true,
                0,
                CancellationToken.None).ConfigureAwait(false);

            uint handle = 0;
            var itemsToCreate = new MonitoredItemCreateRequestCollection
            {
                CreateEventItem(notifierFolder, ++handle),
                CreateEventItem(folderWithoutNotifier, ++handle),
                CreateEventItem(variable, ++handle),
                CreateEventItem(coreVariable, ++handle)
            };

            m_requestHeader.Timestamp = DateTime.UtcNow;
            CreateMonitoredItemsResponse response = await m_server.CreateMonitoredItemsAsync(
                m_secureChannelContext,
                m_requestHeader,
                subscription.SubscriptionId,
                TimestampsToReturn.Both,
                itemsToCreate,
                CancellationToken.None).ConfigureAwait(false);

            Assert.AreEqual((StatusCode)StatusCodes.Good, response.ResponseHeader.ServiceResult);
            Assert.AreEqual(4, response.Results.Count);
            NUnit.Framework.Assert.Multiple(() =>
            {
                Assert.AreEqual((StatusCode)StatusCodes.Good, response.Results[0].StatusCode, "Folder with SubscribeToEvents.");
                Assert.AreEqual(
                    (StatusCode)StatusCodes.BadAttributeIdInvalid,
                    response.Results[1].StatusCode,
                    "Object whose EventNotifier has no SubscribeToEvents bit.");
                Assert.AreEqual(
                    (StatusCode)StatusCodes.BadAttributeIdInvalid,
                    response.Results[2].StatusCode,
                    "Variable: no EventNotifier attribute.");
                Assert.AreEqual(
                    (StatusCode)StatusCodes.BadAttributeIdInvalid,
                    response.Results[3].StatusCode,
                    "Variable in namespace 0: no EventNotifier attribute.");
            });

            m_requestHeader.Timestamp = DateTime.UtcNow;
            await m_server.DeleteSubscriptionsAsync(
                m_secureChannelContext,
                m_requestHeader,
                new UInt32Collection { subscription.SubscriptionId },
                CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// A refused event item must not leave the node flagged as monitored. The flag
        /// is a reference count over the notifier hierarchy (NodeState.AreEventsMonitored):
        /// a node with no active subscription does not need to report events, and a
        /// refused item is never unsubscribed, so a count taken for it is never released.
        /// </summary>
        [Test]
        public async Task RefusedEventItemDoesNotMarkNodeAsMonitoredAsync()
        {
            ushort ns = (ushort)m_server.CurrentInstance.NamespaceUris.GetIndex(
                Quickstarts.ReferenceServer.Namespaces.ReferenceServer);
            // a folder without SubscribeToEvents that no other test subscribes to.
            var folderWithoutNotifier = new NodeId("Attributes", ns);
            Assert.AreEqual(
                EventNotifiers.None,
                await ReadEventNotifierAsync(folderWithoutNotifier).ConfigureAwait(false));
            NodeState folder = m_server.CurrentInstance.NodeManager.NodeManagers
                .OfType<ReferenceNodeManager>()
                .Single()
                .FindPredefinedNode<NodeState>(folderWithoutNotifier);
            Assert.IsNotNull(folder);
            Assert.IsFalse(folder.AreEventsMonitored, "Precondition: no event subscription.");

            m_requestHeader.Timestamp = DateTime.UtcNow;
            CreateSubscriptionResponse subscription = await m_server.CreateSubscriptionAsync(
                m_secureChannelContext,
                m_requestHeader,
                1000,
                100,
                10,
                0,
                true,
                0,
                CancellationToken.None).ConfigureAwait(false);

            m_requestHeader.Timestamp = DateTime.UtcNow;
            CreateMonitoredItemsResponse response = await m_server.CreateMonitoredItemsAsync(
                m_secureChannelContext,
                m_requestHeader,
                subscription.SubscriptionId,
                TimestampsToReturn.Both,
                new MonitoredItemCreateRequestCollection { CreateEventItem(folderWithoutNotifier, 1) },
                CancellationToken.None).ConfigureAwait(false);

            Assert.IsTrue(StatusCode.IsBad(response.Results[0].StatusCode));
            Assert.IsFalse(
                folder.AreEventsMonitored,
                "A refused event item must not mark the node (and its children) as monitored.");

            m_requestHeader.Timestamp = DateTime.UtcNow;
            await m_server.DeleteSubscriptionsAsync(
                m_secureChannelContext,
                m_requestHeader,
                new UInt32Collection { subscription.SubscriptionId },
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsFalse(folder.AreEventsMonitored, "Still flagged after the subscription was deleted.");
        }

        private async Task<byte> ReadEventNotifierAsync(NodeId nodeId)
        {
            m_requestHeader.Timestamp = DateTime.UtcNow;
            ReadResponse response = await m_server.ReadAsync(
                m_secureChannelContext,
                m_requestHeader,
                0,
                TimestampsToReturn.Neither,
                new ReadValueIdCollection
                {
                    new ReadValueId { NodeId = nodeId, AttributeId = Attributes.EventNotifier }
                },
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(StatusCode.IsGood(response.Results[0].StatusCode), $"{nodeId}: {response.Results[0].StatusCode}");
            return Convert.ToByte(response.Results[0].Value);
        }

        private static MonitoredItemCreateRequest CreateEventItem(NodeId nodeId, uint clientHandle)
        {
            return new MonitoredItemCreateRequest
            {
                ItemToMonitor = new ReadValueId
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.EventNotifier
                },
                MonitoringMode = MonitoringMode.Reporting,
                RequestedParameters = new MonitoringParameters
                {
                    ClientHandle = clientHandle,
                    SamplingInterval = 0,
                    QueueSize = 10,
                    DiscardOldest = true,
                    Filter = new ExtensionObject(new EventFilter
                    {
                        SelectClauses =
                        [
                            new SimpleAttributeOperand
                            {
                                TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                BrowsePath = [BrowseNames.EventId],
                                AttributeId = Attributes.Value
                            },
                            new SimpleAttributeOperand
                            {
                                TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                BrowsePath = [BrowseNames.Message],
                                AttributeId = Attributes.Value
                            }
                        ]
                    })
                }
            };
        }
    }
}
