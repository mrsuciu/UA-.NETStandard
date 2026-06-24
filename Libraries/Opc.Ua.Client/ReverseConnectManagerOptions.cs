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

using System.Net;

namespace Opc.Ua.Client
{
    /// <summary>
    /// Optional reverse connect listener limits and binding settings.
    /// </summary>
    /// <remarks>
    /// Reverse connections move through three capacity-controlled stages:
    /// first, an accepted TCP socket waits anonymously for ReverseHello; next, after ReverseHello
    /// identifies the server and the client accepts the connection, it waits in the manager queue;
    /// finally, WaitForConnectionAsync consumes the queued connection to create or reconnect a
    /// session. MaxAnonymousConnections limits the first stage, MaxPendingConnections limits the
    /// whole manager queue, and MaxWaitingConnectionsPerEndpoint limits the part of that queue that
    /// may be used by one server endpoint.
    /// </remarks>
    public class ReverseConnectManagerOptions
    {
        /// <summary>
        /// Maximum number of accepted reverse-connect sockets waiting before ReverseHello.
        /// 0 indicates no limit.
        /// </summary>
        public uint MaxAnonymousConnections { get; set; }

        /// <summary>
        /// Maximum number of identified and accepted reverse connections waiting in the manager
        /// queue. 0 indicates no limit.
        /// </summary>
        public uint MaxPendingConnections { get; set; }

        /// <summary>
        /// Maximum number of queued reverse connections for one server endpoint.
        /// 0 disables queueing.
        /// </summary>
        public uint MaxWaitingConnectionsPerEndpoint { get; set; }

        /// <summary>
        /// Optional IP address to bind the reverse connect listener to.
        /// </summary>
        public IPAddress? ListenAddress { get; set; }
    }
}
