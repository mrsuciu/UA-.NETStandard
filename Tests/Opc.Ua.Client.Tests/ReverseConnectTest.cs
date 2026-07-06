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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Opc.Ua.Server;
using Opc.Ua.Server.Tests;
using Opc.Ua.Tests;
using Quickstarts.ReferenceServer;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Opc.Ua.Client.Tests
{
    /// <summary>
    /// Test Client Reverse Connect Services.
    /// </summary>
    [TestFixture]
    [Category("Client")]
    [SetCulture("en-us")]
    [SetUICulture("en-us")]
    [NonParallelizable]
    public class ReverseConnectTest : ClientTestFramework
    {
        private Uri m_endpointUrl;

        [DatapointSource]
        public static readonly TelemetryParameterizable<ISessionFactory>[] SessionFactories =
        [
            TelemetryParameterizable.Create<ISessionFactory>(t => new TestableSessionFactory(t)),
            TelemetryParameterizable.Create<ISessionFactory>(t => new DefaultSessionFactory(t))
        ];

        /// <summary>
        /// Setup a server and client fixture.
        /// </summary>
        [OneTimeSetUp]
        public override async Task OneTimeSetUpAsync()
        {
            // this test fails on macOS, ignore (TODO)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                NUnit.Framework.Assert.Ignore("Reverse connect fails on mac OS.");
            }

            // pki directory root for test runs.
            PkiRoot = Path.GetTempPath() + Path.GetRandomFileName();

            // start ref server with reverse connect
            ServerFixture = new ServerFixture<ReferenceServer>
            {
                AutoAccept = true,
                SecurityNone = true,
                ReverseConnectTimeout = MaxTimeout,
                TraceMasks = Utils.TraceMasks.Error | Utils.TraceMasks.Security
            };
            ReferenceServer = await ServerFixture.StartAsync(PkiRoot)
                .ConfigureAwait(false);

            // create client
            ClientFixture = new ClientFixture(telemetry: Telemetry);

            await ClientFixture.LoadClientConfigurationAsync(PkiRoot).ConfigureAwait(false);
            await ClientFixture.StartReverseConnectHostAsync().ConfigureAwait(false);
            m_endpointUrl = new Uri(
                Utils.ReplaceLocalhost(
                    "opc.tcp://localhost:" +
                    ServerFixture.Port.ToString(CultureInfo.InvariantCulture)));
            // start reverse connection
            ReferenceServer.AddReverseConnection(
                new Uri(ClientFixture.ReverseConnectUri),
                MaxTimeout);
        }

        /// <summary>
        /// Tear down the Server and the Client.
        /// </summary>
        [OneTimeTearDown]
        public override Task OneTimeTearDownAsync()
        {
            Utils.SilentDispose(ClientFixture);
            return base.OneTimeTearDownAsync();
        }

        /// <summary>
        /// Test setup.
        /// </summary>
        [SetUp]
        public override Task SetUpAsync()
        {
            return base.SetUpAsync();
        }

        /// <summary>
        /// Test teardown.
        /// </summary>
        [TearDown]
        public override Task TearDownAsync()
        {
            return base.TearDownAsync();
        }

        /// <summary>
        /// Get endpoints using a reverse connection.
        /// </summary>
        [Test]
        [Order(100)]
        public async Task GetEndpointsAsync()
        {
            await RequireEndpointsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Internal get endpoints which is called with semaphore.
        /// </summary>
        public async Task GetEndpointsInternalAsync()
        {
            ApplicationConfiguration config = ClientFixture.Config;
            ITransportWaitingConnection connection;
            using (var cancellationTokenSource = new CancellationTokenSource(MaxTimeout))
            {
                connection = await ClientFixture
                    .ReverseConnectManager.WaitForConnectionAsync(
                        m_endpointUrl,
                        null,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                Assert.NotNull(connection, "Failed to get connection.");
            }

            using (var cancellationTokenSource = new CancellationTokenSource(MaxTimeout))
            {
                var endpointConfiguration = EndpointConfiguration.Create();
                endpointConfiguration.OperationTimeout = MaxTimeout;
                using DiscoveryClient client = await DiscoveryClient.CreateAsync(
                    config,
                    connection,
                    endpointConfiguration,
                    ct: cancellationTokenSource.Token).ConfigureAwait(false);
                Endpoints = await client.GetEndpointsAsync(null, cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                await client.CloseAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            }
        }

        [Test]
        [Order(200)]
        public async Task SelectEndpointAsync()
        {
            ApplicationConfiguration config = ClientFixture.Config;
            ITransportWaitingConnection connection;
            using (var cancellationTokenSource = new CancellationTokenSource(MaxTimeout))
            {
                connection = await ClientFixture
                    .ReverseConnectManager.WaitForConnectionAsync(
                        m_endpointUrl,
                        null,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                Assert.NotNull(connection, "Failed to get connection.");
            }
            EndpointDescription selectedEndpoint = await CoreClientUtils.SelectEndpointAsync(
                config,
                connection,
                true,
                MaxTimeout,
                Telemetry).ConfigureAwait(false);
            Assert.NotNull(selectedEndpoint);
        }

        [Test]
        [Order(250)]
        public async Task QueuedReverseConnectionAsync()
        {
            using var queuedClientFixture = new ClientFixture(telemetry: Telemetry);
            await queuedClientFixture
                .LoadClientConfigurationAsync(PkiRoot, "QueuedReverseConnectClient")
                .ConfigureAwait(false);
            queuedClientFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    MaxAnonymousConnections = 2,
                    MaxPendingConnections = 1,
                    MaxWaitingConnectionsPerEndpoint = 1,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await queuedClientFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(queuedClientFixture.ReverseConnectUri);
            ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 1);
            try
            {
                await Task.Delay(MaxTimeout / 2).ConfigureAwait(false);

                using var cancellationTokenSource = new CancellationTokenSource(MaxTimeout);
                Stopwatch stopwatch = Stopwatch.StartNew();
                ITransportWaitingConnection connection = await queuedClientFixture
                    .ReverseConnectManager
                    .WaitForConnectionAsync(
                        m_endpointUrl,
                        null,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                Assert.NotNull(connection, "Failed to get queued connection.");
                Assert.Less(
                    stopwatch.ElapsedMilliseconds,
                    1000,
                    "Queued connection should be returned without waiting for another ReverseHello.");
                Utils.SilentDispose(connection.Handle as IDisposable);
            }
            finally
            {
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);
            }
        }

        [Test]
        [Order(251)]
        public async Task MaxAnonymousConnectionsRejectsExcessSocketAsync()
        {
            using var limitedClientFixture = new ClientFixture(telemetry: Telemetry);
            await limitedClientFixture
                .LoadClientConfigurationAsync(PkiRoot, "AnonymousLimitReverseConnectClient")
                .ConfigureAwait(false);
            limitedClientFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    MaxAnonymousConnections = 1,
                    MaxPendingConnections = 1,
                    MaxWaitingConnectionsPerEndpoint = 1,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await limitedClientFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(limitedClientFixture.ReverseConnectUri);
            using var firstClient = new TcpClient(AddressFamily.InterNetwork);
            using var secondClient = new TcpClient(AddressFamily.InterNetwork);
            await firstClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);
            await secondClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);

            Assert.IsTrue(
                await WaitForSocketClosedAsync(secondClient.Client, 3000).ConfigureAwait(false),
                "The second anonymous reverse connection should be closed when the limit is reached.");
        }

        [Test]
        [Order(252)]
        public async Task MaxClientChannelsRejectsExcessAnonymousSocketAsync()
        {
            using var limitedClientFixture = new ClientFixture(telemetry: Telemetry);
            await limitedClientFixture
                .LoadClientConfigurationAsync(PkiRoot, "ClientChannelLimitReverseConnectClient")
                .ConfigureAwait(false);
            limitedClientFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    MaxClientChannels = 1,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await limitedClientFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(limitedClientFixture.ReverseConnectUri);
            using var firstClient = new TcpClient(AddressFamily.InterNetwork);
            using var secondClient = new TcpClient(AddressFamily.InterNetwork);
            await firstClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);
            await secondClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);

            Assert.IsTrue(
                await WaitForSocketClosedAsync(secondClient.Client, 3000).ConfigureAwait(false),
                "The second reverse connection should be closed when MaxClientChannels is reached.");
        }

        [Test]
        [Order(253)]
        public async Task MaxClientChannelsAllowsConfiguredCapacityBeforeRejectingAsync()
        {
            using var limitedClientFixture = new ClientFixture(telemetry: Telemetry);
            await limitedClientFixture
                .LoadClientConfigurationAsync(PkiRoot, "ClientChannelCapacityReverseConnectClient")
                .ConfigureAwait(false);
            limitedClientFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    MaxClientChannels = 2,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await limitedClientFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(limitedClientFixture.ReverseConnectUri);
            using var firstClient = new TcpClient(AddressFamily.InterNetwork);
            using var secondClient = new TcpClient(AddressFamily.InterNetwork);
            using var thirdClient = new TcpClient(AddressFamily.InterNetwork);
            await firstClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);
            await secondClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);
            await thirdClient
                .ConnectAsync(IPAddress.Loopback, reverseConnectUri.Port)
                .ConfigureAwait(false);

            Assert.IsFalse(
                await WaitForSocketClosedAsync(firstClient.Client, 500).ConfigureAwait(false),
                "The first reverse connection should remain open within MaxClientChannels capacity.");
            Assert.IsFalse(
                await WaitForSocketClosedAsync(secondClient.Client, 500).ConfigureAwait(false),
                "The second reverse connection should remain open within MaxClientChannels capacity.");
            Assert.IsTrue(
                await WaitForSocketClosedAsync(thirdClient.Client, 3000).ConfigureAwait(false),
                "The third reverse connection should be closed after MaxClientChannels capacity is reached.");
        }

        [Test]
        [Order(254)]
        public async Task MaxClientChannelsRejectsExcessQueuedReverseHelloAsync()
        {
            using var clientChannelLimitFixture = new ClientFixture(telemetry: Telemetry);
            await clientChannelLimitFixture
                .LoadClientConfigurationAsync(PkiRoot, "ClientChannelQueuedLimitReverseConnectClient")
                .ConfigureAwait(false);
            clientChannelLimitFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    HoldTime = 100,
                    WaitTimeout = 1000,
                    MaxClientChannels = 1,
                    MaxPendingConnections = 2,
                    MaxWaitingConnectionsPerEndpoint = 2,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await clientChannelLimitFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(clientChannelLimitFixture.ReverseConnectUri);
            ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 2);
            try
            {
                using var cancellationTokenSource = new CancellationTokenSource(MaxTimeout);
                ITransportWaitingConnection connection = await clientChannelLimitFixture
                    .ReverseConnectManager
                    .WaitForConnectionAsync(
                        m_endpointUrl,
                        null,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                Assert.NotNull(connection, "The first queued connection should remain available.");
                using (var secondCancellationTokenSource = new CancellationTokenSource(500))
                {
                    ServiceResultException exception = Assert.ThrowsAsync<ServiceResultException>(
                        async () => await clientChannelLimitFixture
                            .ReverseConnectManager
                            .WaitForConnectionAsync(
                                m_endpointUrl,
                                null,
                                secondCancellationTokenSource.Token)
                            .ConfigureAwait(false));
                    Assert.AreEqual(StatusCodes.BadTimeout, exception.StatusCode);
                }
                Utils.SilentDispose(connection.Handle as IDisposable);
            }
            finally
            {
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);
            }
        }

        [Test]
        [Order(255)]
        public async Task MaxClientChannelsReleasesAfterQueuedConnectionDisposedAsync()
        {
            using var clientChannelReleaseFixture = new ClientFixture(telemetry: Telemetry);
            await clientChannelReleaseFixture
                .LoadClientConfigurationAsync(PkiRoot, "ClientChannelReleaseReverseConnectClient")
                .ConfigureAwait(false);
            clientChannelReleaseFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    HoldTime = 100,
                    WaitTimeout = 1000,
                    MaxClientChannels = 1,
                    MaxPendingConnections = 1,
                    MaxWaitingConnectionsPerEndpoint = 1,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await clientChannelReleaseFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(clientChannelReleaseFixture.ReverseConnectUri);
            try
            {
                ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 1);
                using (var cancellationTokenSource = new CancellationTokenSource(MaxTimeout))
                {
                    ITransportWaitingConnection connection = await clientChannelReleaseFixture
                        .ReverseConnectManager
                        .WaitForConnectionAsync(
                            m_endpointUrl,
                            null,
                            cancellationTokenSource.Token)
                        .ConfigureAwait(false);

                    Assert.NotNull(connection, "The first queued connection should be available.");
                    Utils.SilentDispose(connection.Handle as IDisposable);
                }
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);

                ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 1);
                using (var cancellationTokenSource = new CancellationTokenSource(MaxTimeout))
                {
                    ITransportWaitingConnection connection = await clientChannelReleaseFixture
                        .ReverseConnectManager
                        .WaitForConnectionAsync(
                            m_endpointUrl,
                            null,
                            cancellationTokenSource.Token)
                        .ConfigureAwait(false);

                    Assert.NotNull(
                        connection,
                        "A new reverse connection should be accepted after the first handle is disposed.");
                    Utils.SilentDispose(connection.Handle as IDisposable);
                }
            }
            finally
            {
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);
            }
        }

        [Test]
        [Order(256)]
        public async Task MaxClientChannelsReleasesAfterSessionCloseAsync()
        {
            using var clientChannelSessionFixture = new ClientFixture(telemetry: Telemetry);
            await clientChannelSessionFixture
                .LoadClientConfigurationAsync(PkiRoot, "ClientChannelSessionReverseConnectClient")
                .ConfigureAwait(false);
            clientChannelSessionFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    MaxClientChannels = 1,
                    ListenAddress = IPAddress.Loopback.ToString()
            };
            await clientChannelSessionFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            ApplicationConfiguration config = clientChannelSessionFixture.Config;
            EndpointDescriptionCollection endpoints = await clientChannelSessionFixture
                .GetEndpointsAsync(m_endpointUrl)
                .ConfigureAwait(false);
            var endpointConfiguration = EndpointConfiguration.Create(config);
            EndpointDescription selectedEndpoint = ClientFixture.SelectEndpoint(
                config,
                endpoints,
                m_endpointUrl,
                SecurityPolicies.None);
            Assert.NotNull(selectedEndpoint);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            Assert.NotNull(endpoint);

            var sessionFactory = new DefaultSessionFactory(Telemetry);
            var reverseConnectUri = new Uri(clientChannelSessionFixture.ReverseConnectUri);
            ISession session = null;
            ISession secondSession = null;
            try
            {
                ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 1);
                session = await sessionFactory
                    .CreateAsync(
                        config,
                        clientChannelSessionFixture.ReverseConnectManager,
                        endpoint,
                        false,
                        false,
                        "Reverse Connect MaxClientChannels Session",
                        MaxTimeout,
                        new UserIdentity(),
                        null)
                    .ConfigureAwait(false);
                Assert.NotNull(session);

                await session.CloseAsync().ConfigureAwait(false);
                session.Dispose();
                session = null;
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);

                ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 1);
                secondSession = await sessionFactory
                    .CreateAsync(
                        config,
                        clientChannelSessionFixture.ReverseConnectManager,
                        endpoint,
                        false,
                        false,
                        "Reverse Connect MaxClientChannels Session 2",
                        MaxTimeout,
                        new UserIdentity(),
                        null)
                    .ConfigureAwait(false);
                Assert.NotNull(secondSession);
            }
            finally
            {
                if (secondSession != null)
                {
                    await secondSession.CloseAsync().ConfigureAwait(false);
                    secondSession.Dispose();
                }
                if (session != null)
                {
                    await session.CloseAsync().ConfigureAwait(false);
                    session.Dispose();
                }
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);
            }
        }

        [Test]
        [Order(257)]
        public async Task MaxPendingConnectionsRejectsExcessQueuedReverseHelloAsync()
        {
            using var pendingLimitClientFixture = new ClientFixture(telemetry: Telemetry);
            await pendingLimitClientFixture
                .LoadClientConfigurationAsync(PkiRoot, "PendingLimitReverseConnectClient")
                .ConfigureAwait(false);
            pendingLimitClientFixture.Config.ClientConfiguration.ReverseConnect =
                new ReverseConnectClientConfiguration
                {
                    HoldTime = 100,
                    WaitTimeout = 1000,
                    MaxAnonymousConnections = 2,
                    MaxPendingConnections = 1,
                    MaxWaitingConnectionsPerEndpoint = 2,
                    ListenAddress = IPAddress.Loopback.ToString()
                };
            await pendingLimitClientFixture.StartReverseConnectHostAsync("127.0.0.1").ConfigureAwait(false);

            var reverseConnectUri = new Uri(pendingLimitClientFixture.ReverseConnectUri);
            ReferenceServer.AddReverseConnection(reverseConnectUri, MaxTimeout, maxSessionCount: 2);
            try
            {
                Assert.IsTrue(
                    await WaitForRejectedReverseConnectionAsync(reverseConnectUri).ConfigureAwait(false),
                    "A second unmatched ReverseHello should be rejected when MaxPendingConnections is full.");

                using var cancellationTokenSource = new CancellationTokenSource(MaxTimeout);
                ITransportWaitingConnection connection = await pendingLimitClientFixture
                    .ReverseConnectManager
                    .WaitForConnectionAsync(
                        m_endpointUrl,
                        null,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                Assert.NotNull(connection, "The first queued connection should remain available.");
                Utils.SilentDispose(connection.Handle as IDisposable);
            }
            finally
            {
                ReferenceServer.RemoveReverseConnection(reverseConnectUri);
            }
        }

        [Theory]
        [Order(300)]
        public async Task ReverseConnectAsync(string securityPolicy, TelemetryParameterizable<ISessionFactory> sessionFactory)
        {
            ITelemetryContext telemetry = NUnitTelemetryContext.Create();

            // ensure endpoints are available
            await RequireEndpointsAsync().ConfigureAwait(false);

            // get a connection
            ApplicationConfiguration config = ClientFixture.Config;
            ITransportWaitingConnection connection;
            using (var cancellationTokenSource = new CancellationTokenSource(MaxTimeout))
            {
                connection = await ClientFixture
                    .ReverseConnectManager.WaitForConnectionAsync(
                        m_endpointUrl,
                        null,
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);
                Assert.NotNull(connection, "Failed to get connection.");
            }

            // select the secure endpoint
            var endpointConfiguration = EndpointConfiguration.Create(config);
            EndpointDescription selectedEndpoint = ClientFixture.SelectEndpoint(
                config,
                Endpoints,
                m_endpointUrl,
                securityPolicy);
            Assert.NotNull(selectedEndpoint);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            Assert.NotNull(endpoint);

            // connect
            ISession session = await sessionFactory.Create(telemetry)
                .CreateAsync(
                    config,
                    connection,
                    endpoint,
                    false,
                    false,
                    "Reverse Connect Client",
                    MaxTimeout,
                    new UserIdentity(),
                    null)
                .ConfigureAwait(false);
            Assert.NotNull(session);

            // default request header
            var requestHeader = new RequestHeader
            {
                Timestamp = DateTime.UtcNow,
                TimeoutHint = MaxTimeout
            };

            // Browse
            var clientTestServices = new ClientTestServices(session, telemetry);
            ReferenceDescriptionCollection referenceDescriptions = await CommonTestWorkers
                .BrowseFullAddressSpaceWorkerAsync(
                    clientTestServices,
                    requestHeader)
                .ConfigureAwait(false);
            Assert.NotNull(referenceDescriptions);

            // close session
            StatusCode result = await session.CloseAsync().ConfigureAwait(false);
            Assert.NotNull(result);
            session.Dispose();
        }

        [Theory]
        [Order(301)]
        public async Task ReverseConnect2Async(
            bool updateBeforeConnect,
            bool checkDomain,
            TelemetryParameterizable<ISessionFactory> sessionFactory)
        {
            ITelemetryContext telemetry = NUnitTelemetryContext.Create();

            const string securityPolicy = SecurityPolicies.Basic256Sha256;

            // ensure endpoints are available
            await RequireEndpointsAsync().ConfigureAwait(false);

            // get a connection
            ApplicationConfiguration config = ClientFixture.Config;

            // select the secure endpoint
            var endpointConfiguration = EndpointConfiguration.Create(config);
            EndpointDescription selectedEndpoint = ClientFixture.SelectEndpoint(
                config,
                Endpoints,
                m_endpointUrl,
                securityPolicy);
            Assert.NotNull(selectedEndpoint);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            Assert.NotNull(endpoint);

            // connect
            ISession session = await sessionFactory.Create(telemetry)
                .CreateAsync(
                    config,
                    ClientFixture.ReverseConnectManager,
                    endpoint,
                    updateBeforeConnect,
                    checkDomain,
                    "Reverse Connect Client",
                    MaxTimeout,
                    new UserIdentity(),
                    null)
                .ConfigureAwait(false);

            Assert.NotNull(session);

            // header
            var requestHeader = new RequestHeader
            {
                Timestamp = DateTime.UtcNow,
                TimeoutHint = MaxTimeout
            };

            // Browse
            var clientTestServices = new ClientTestServices(session, telemetry);
            ReferenceDescriptionCollection referenceDescriptions = await CommonTestWorkers
                .BrowseFullAddressSpaceWorkerAsync(
                    clientTestServices,
                    requestHeader)
                .ConfigureAwait(false);
            Assert.NotNull(referenceDescriptions);

            // close session
            StatusCode result = await session.CloseAsync().ConfigureAwait(false);
            Assert.NotNull(result);
            session.Dispose();
        }

        private async Task RequireEndpointsAsync()
        {
            await m_requiredLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Endpoints == null)
                {
                    await GetEndpointsInternalAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                m_requiredLock.Release();
            }
        }

        private static async Task<bool> WaitForSocketClosedAsync(Socket socket, int timeout)
        {
            int startTime = HiResClock.TickCount;
            while (HiResClock.TickCount - startTime < timeout)
            {
                if (socket.Poll(100_000, SelectMode.SelectRead) && socket.Available == 0)
                {
                    return true;
                }
                await Task.Delay(50).ConfigureAwait(false);
            }

            return false;
        }

        private async Task<bool> WaitForRejectedReverseConnectionAsync(Uri reverseConnectUri)
        {
            int startTime = HiResClock.TickCount;
            while (HiResClock.TickCount - startTime < MaxTimeout)
            {
                if (ReferenceServer.GetReverseConnections().TryGetValue(
                        reverseConnectUri,
                        out ReverseConnectProperty reverseConnection) &&
                    reverseConnection.LastState == ReverseConnectState.Rejected)
                {
                    return true;
                }
                await Task.Delay(100).ConfigureAwait(false);
            }

            return false;
        }

        private readonly SemaphoreSlim m_requiredLock = new(1);
    }
}
