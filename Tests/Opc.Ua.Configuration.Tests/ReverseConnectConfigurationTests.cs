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

using System.IO;
using System.Runtime.Serialization;
using NUnit.Framework;
using Opc.Ua.Tests;

namespace Opc.Ua.Configuration.Tests
{
    [TestFixture]
    [Category("Configuration")]
    public class ReverseConnectConfigurationTests
    {
        [Test]
        public void ReverseConnectConfigurationDefaultsAreCompatible()
        {
            var reverseConnect = new ReverseConnectClientConfiguration();

            Assert.That(reverseConnect.MaxAnonymousConnections, Is.EqualTo(0));
            Assert.That(reverseConnect.MaxPendingConnections, Is.EqualTo(0));
            Assert.That(reverseConnect.MaxWaitingConnectionsPerEndpoint, Is.EqualTo(0));
            Assert.That(reverseConnect.ListenAddress, Is.Null);
        }

        [Test]
        public void ReverseConnectConfigurationRoundTripsNewLimits()
        {
            var configuration = new ApplicationConfiguration(NUnitTelemetryContext.Create())
            {
                ApplicationName = "ReverseConnectConfigurationTest",
                ApplicationUri = "urn:localhost:ReverseConnectConfigurationTest",
                ApplicationType = ApplicationType.Client,
                ClientConfiguration = new ClientConfiguration
                {
                    ReverseConnect = new ReverseConnectClientConfiguration
                    {
                        MaxAnonymousConnections = 1,
                        MaxPendingConnections = 2,
                        MaxWaitingConnectionsPerEndpoint = 3,
                        ListenAddress = "127.0.0.1"
                    }
                }
            };

            var serializer = new DataContractSerializer(typeof(ApplicationConfiguration));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, configuration);
            stream.Position = 0;

            var reloadedConfiguration = (ApplicationConfiguration)serializer.ReadObject(stream);
            ReverseConnectClientConfiguration reverseConnect =
                reloadedConfiguration.ClientConfiguration.ReverseConnect;

            Assert.That(reverseConnect.MaxAnonymousConnections, Is.EqualTo(1));
            Assert.That(reverseConnect.MaxPendingConnections, Is.EqualTo(2));
            Assert.That(reverseConnect.MaxWaitingConnectionsPerEndpoint, Is.EqualTo(3));
            Assert.That(reverseConnect.ListenAddress, Is.EqualTo("127.0.0.1"));
        }

        [Test]
        public void FluentBuilderSetsReverseConnectLimits()
        {
            ITelemetryContext telemetry = NUnitTelemetryContext.Create();
            var application = new ApplicationInstance(telemetry)
            {
                ApplicationName = "ReverseConnectBuilderTest"
            };

            application
                .Build(
                    "urn:localhost:ReverseConnectBuilderTest",
                    "uri:ReverseConnectBuilderTest")
                .AsClient()
                .SetReverseConnect(
                    new ReverseConnectClientConfiguration
                    {
                        MaxAnonymousConnections = 1,
                        MaxPendingConnections = 2,
                        MaxWaitingConnectionsPerEndpoint = 3,
                        ListenAddress = "127.0.0.1"
                    });

            ReverseConnectClientConfiguration reverseConnect =
                application.ApplicationConfiguration.ClientConfiguration.ReverseConnect;

            Assert.That(reverseConnect.MaxAnonymousConnections, Is.EqualTo(1));
            Assert.That(reverseConnect.MaxPendingConnections, Is.EqualTo(2));
            Assert.That(reverseConnect.MaxWaitingConnectionsPerEndpoint, Is.EqualTo(3));
            Assert.That(reverseConnect.ListenAddress, Is.EqualTo("127.0.0.1"));
        }
    }
}
