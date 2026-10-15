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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Quickstarts.ReferenceServer;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Opc.Ua.Server.Tests
{
    /// <summary>
    /// CTT observation "Optional nodes in instances": the HA Configuration of the
    /// historizing Reference Server variables, as a client sees it through Browse
    /// and Read.
    /// </summary>
    /// <remarks>
    /// OPC 10000-11 5.2.2 HistoricalDataConfigurationType: AggregateConfiguration and
    /// Stepped are Mandatory, all other children are Optional. An Optional child that
    /// is exposed states a property of the archive, so its value must be true:
    /// MinTimeInterval/MaxTimeInterval "the minimum/maximum interval between data
    /// points in the history repository regardless of their value change";
    /// MaxCountStoredValues "the maximum number of values that can be stored for a
    /// single point"; MaxTimeStoredValues "the maximum time period that is available
    /// for historical data"; Definition "a vendor-specific, human readable string
    /// that specifies how the value ... is calculated"; ExceptionDeviation "the
    /// minimum amount that the data ... shall change in order for the change to be
    /// reported to the history database"; AggregateFunctions "an entry point to
    /// browse to all Aggregate capabilities supported by the Server".
    /// The Reference Server archive (Quickstarts.Servers TestData.HistoryArchive)
    /// stores one value every 10 seconds regardless of value changes, keeps at most
    /// 2000 values per variable and has no time based retention.
    /// </remarks>
    [TestFixture]
    [Category("Server")]
    [Category("HistoryRead")]
    [SetCulture("en-us")]
    [SetUICulture("en-us")]
    public class HistoricalConfigurationCttTests
    {
        private const double kSampleIntervalMs = 10000;
        private const uint kMaxStoredValues = 2000;

        private static readonly string[] s_supportedChildren =
        [
            BrowseNames.AggregateConfiguration,
            BrowseNames.Stepped,
            BrowseNames.MinTimeInterval,
            BrowseNames.MaxTimeInterval,
            BrowseNames.StartOfArchive,
            BrowseNames.StartOfOnlineArchive,
            BrowseNames.ServerTimestampSupported,
            BrowseNames.MaxCountStoredValues
        ];

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
                DurableSubscriptionsEnabled = false,
                UseSamplingGroupsInReferenceNodeManager = false
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
        /// The HA Configuration exposes only children with a true value: the mandatory
        /// ones, the archive interval and capacity, the archive start and the
        /// ServerTimestamp support. Every exposed variable reads Good with a value.
        /// </summary>
        [TestCase("Scalar_Static_Int32")]
        [TestCase("Scalar_Static_Boolean")]
        [TestCase("Aggregates_Double")]
        public async Task HistoricalConfigurationExposesOnlySupportedOptionalChildrenAsync(string identifier)
        {
            var variableId = new NodeId(
                identifier,
                (ushort)m_server.CurrentInstance.NamespaceUris.GetIndex(
                    Quickstarts.ReferenceServer.Namespaces.ReferenceServer));

            ReferenceDescriptionCollection configurations = await BrowseAsync(
                variableId,
                ReferenceTypeIds.HasHistoricalConfiguration).ConfigureAwait(false);
            Assert.AreEqual(1, configurations.Count, "Exactly one HasHistoricalConfiguration reference.");
            NodeId configurationId = ExpandedNodeId.ToNodeId(
                configurations[0].NodeId,
                m_server.CurrentInstance.NamespaceUris);

            ReferenceDescriptionCollection children = await BrowseAsync(
                configurationId,
                ReferenceTypeIds.HierarchicalReferences).ConfigureAwait(false);
            string[] childNames = [.. children.Select(c => c.BrowseName.Name).OrderBy(n => n, StringComparer.Ordinal)];
            Assert.AreEqual(
                string.Join(", ", s_supportedChildren.OrderBy(n => n, StringComparer.Ordinal)),
                string.Join(", ", childNames),
                "Children of the HA Configuration.");

            Dictionary<string, DataValue> values = await ReadValuesAsync(
                children.Where(c => c.NodeClass == NodeClass.Variable)).ConfigureAwait(false);
            foreach (KeyValuePair<string, DataValue> value in values)
            {
                Assert.IsTrue(StatusCode.IsGood(value.Value.StatusCode), $"{value.Key}: {value.Value.StatusCode}");
                Assert.IsNotNull(value.Value.Value, $"{value.Key} has no value.");
            }

            Assert.AreEqual(kSampleIntervalMs, values[BrowseNames.MinTimeInterval].Value);
            Assert.AreEqual(kSampleIntervalMs, values[BrowseNames.MaxTimeInterval].Value);
            Assert.AreEqual(kMaxStoredValues, values[BrowseNames.MaxCountStoredValues].Value);
        }

        private async Task<ReferenceDescriptionCollection> BrowseAsync(NodeId nodeId, NodeId referenceTypeId)
        {
            m_requestHeader.Timestamp = DateTime.UtcNow;
            BrowseResponse response = await m_server.BrowseAsync(
                m_secureChannelContext,
                m_requestHeader,
                null,
                0,
                new BrowseDescriptionCollection
                {
                    new BrowseDescription
                    {
                        NodeId = nodeId,
                        BrowseDirection = BrowseDirection.Forward,
                        ReferenceTypeId = referenceTypeId,
                        IncludeSubtypes = true,
                        NodeClassMask = 0,
                        ResultMask = (uint)BrowseResultMask.All
                    }
                },
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsTrue(StatusCode.IsGood(response.Results[0].StatusCode));
            return response.Results[0].References;
        }

        private async Task<Dictionary<string, DataValue>> ReadValuesAsync(
            IEnumerable<ReferenceDescription> variables)
        {
            ReferenceDescription[] list = [.. variables];
            var nodesToRead = new ReadValueIdCollection();
            foreach (ReferenceDescription variable in list)
            {
                nodesToRead.Add(new ReadValueId
                {
                    NodeId = ExpandedNodeId.ToNodeId(variable.NodeId, m_server.CurrentInstance.NamespaceUris),
                    AttributeId = Attributes.Value
                });
            }

            m_requestHeader.Timestamp = DateTime.UtcNow;
            ReadResponse response = await m_server.ReadAsync(
                m_secureChannelContext,
                m_requestHeader,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                CancellationToken.None).ConfigureAwait(false);

            var values = new Dictionary<string, DataValue>(StringComparer.Ordinal);
            for (int ii = 0; ii < list.Length; ii++)
            {
                values[list[ii].BrowseName.Name] = response.Results[ii];
            }

            return values;
        }
    }
}
