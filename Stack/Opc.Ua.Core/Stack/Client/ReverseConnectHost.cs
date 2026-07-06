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
using System.Net;
using Microsoft.Extensions.Logging;
using Opc.Ua.Bindings;

namespace Opc.Ua
{
    /// <summary>
    /// Reverse Connect Client Host.
    /// </summary>
    public class ReverseConnectHost
    {
        /// <summary>
        /// Create reverse connect host
        /// </summary>
        /// <param name="telemetry">Telemetry context to use</param>
        public ReverseConnectHost(ITelemetryContext telemetry)
        {
            m_telemetry = telemetry;
            m_logger = telemetry.CreateLogger<ReverseConnectHost>();
        }

        /// <summary>
        /// Creates a new reverse listener host for a client with default listener behavior.
        /// </summary>
        /// <param name="url">The reverse connect endpoint URL where the client listens.</param>
        /// <param name="onConnectionWaiting">The callback raised when a ReverseHello connection is ready.</param>
        /// <param name="onConnectionStatusChanged">The callback raised when a reverse connection changes state.</param>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> is <c>null</c>.</exception>
        /// <exception cref="ServiceResultException"></exception>
        public void CreateListener(
            Uri url,
            ConnectionWaitingHandlerAsync onConnectionWaiting,
            EventHandler<ConnectionStatusEventArgs> onConnectionStatusChanged)
        {
            CreateListener(url, onConnectionWaiting, onConnectionStatusChanged, 0, 0, null);
        }

        /// <summary>
        /// Creates a new reverse listener host for a client with listener-specific limits.
        /// </summary>
        /// <param name="url">The reverse connect endpoint URL where the client listens.</param>
        /// <param name="onConnectionWaiting">The callback raised when a ReverseHello connection is ready.</param>
        /// <param name="onConnectionStatusChanged">The callback raised when a reverse connection changes state.</param>
        /// <param name="maxAnonymousConnections">
        /// The maximum number of accepted sockets that may wait for ReverseHello at the same time.
        /// A value of 0 disables this limit.
        /// </param>
        /// <param name="listenAddress">The optional IP address to bind the listener to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> is <c>null</c>.</exception>
        /// <exception cref="ServiceResultException"></exception>
        public void CreateListener(
            Uri url,
            ConnectionWaitingHandlerAsync onConnectionWaiting,
            EventHandler<ConnectionStatusEventArgs> onConnectionStatusChanged,
            uint maxAnonymousConnections,
            IPAddress listenAddress)
        {
            CreateListener(
                url,
                onConnectionWaiting,
                onConnectionStatusChanged,
                0,
                maxAnonymousConnections,
                listenAddress);
        }

        /// <summary>
        /// Creates a new reverse listener host for a client with listener-specific limits.
        /// </summary>
        /// <param name="url">The reverse connect endpoint URL where the client listens.</param>
        /// <param name="onConnectionWaiting">The callback raised when a ReverseHello connection is ready.</param>
        /// <param name="onConnectionStatusChanged">The callback raised when a reverse connection changes state.</param>
        /// <param name="maxClientChannels">
        /// The maximum number of reverse-connect sockets/channels that may exist through this
        /// listener at the same time. A value of 0 disables this limit.
        /// </param>
        /// <param name="maxAnonymousConnections">
        /// The maximum number of accepted sockets that may wait for ReverseHello at the same time.
        /// A value of 0 disables this limit.
        /// </param>
        /// <param name="listenAddress">The optional IP address to bind the listener to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="url"/> is <c>null</c>.</exception>
        /// <exception cref="ServiceResultException"></exception>
        public void CreateListener(
            Uri url,
            ConnectionWaitingHandlerAsync onConnectionWaiting,
            EventHandler<ConnectionStatusEventArgs> onConnectionStatusChanged,
            uint maxClientChannels,
            uint maxAnonymousConnections,
            IPAddress listenAddress)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            ITransportListener listener = TransportBindings.Listeners.Create(
                url.Scheme,
                m_telemetry);

            m_listener =
                listener
                ?? throw ServiceResultException.Create(
                    StatusCodes.BadProtocolVersionUnsupported,
                    "Unsupported transport profile for scheme {0}.",
                    url.Scheme);
            Url = url;
            m_onConnectionWaiting = onConnectionWaiting;
            m_onConnectionStatusChanged = onConnectionStatusChanged;
            m_maxClientChannels = maxClientChannels;
            m_maxAnonymousConnections = maxAnonymousConnections;
            m_listenAddress = listenAddress;
        }

        /// <summary>
        /// The Url which is used by the transport listener.
        /// </summary>
        public Uri Url { get; private set; }

        /// <summary>
        /// Opens a reverse listener host.
        /// </summary>
        public void Open()
        {
            // create the UA listener.
            try
            {
                var settings = new TransportListenerSettings
                {
                    Descriptions = null,
                    Configuration = null,
                    CertificateValidator = null,
                    NamespaceUris = null,
                    Factory = null,
                    ReverseConnectListener = true,
                    MaxChannelCount = 0,
                    MaxReverseConnectClientChannels = m_maxClientChannels,
                    MaxReverseConnectAnonymousConnections = m_maxAnonymousConnections,
                    ListenAddress = m_listenAddress
                };

                m_logger.LogInformation("Open reverse connect listener for {Url}.", Url);

                m_listener.Open(Url, settings, null);

                m_listener.ConnectionWaiting += m_onConnectionWaiting;
                m_listener.ConnectionStatusChanged += m_onConnectionStatusChanged;
            }
            catch (Exception e)
            {
                m_logger.LogError(e, "Could not open listener for {Url}.", Url);
                throw;
            }
        }

        /// <summary>
        /// Close the reverse connect listener.
        /// </summary>
        public void Close()
        {
            m_listener.ConnectionWaiting -= m_onConnectionWaiting;
            m_listener.ConnectionStatusChanged -= m_onConnectionStatusChanged;
            m_listener.Close();
        }

        private ITransportListener m_listener;
        private ConnectionWaitingHandlerAsync m_onConnectionWaiting;
        private EventHandler<ConnectionStatusEventArgs> m_onConnectionStatusChanged;
        private readonly ITelemetryContext m_telemetry;
        private readonly ILogger m_logger;
        private uint m_maxClientChannels;
        private uint m_maxAnonymousConnections;
        private IPAddress m_listenAddress;
    }
}
