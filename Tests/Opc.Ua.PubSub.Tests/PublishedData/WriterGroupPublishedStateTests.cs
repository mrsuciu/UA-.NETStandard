/* ========================================================================
 * Copyright (c) 2005-2021 The OPC Foundation, Inc. All rights reserved.
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
using System.Text;
using System.Linq;
using NUnit.Framework;
using Opc.Ua.PubSub.Encoding;
using Opc.Ua.PubSub.Tests.Encoding;
using Opc.Ua.PubSub.PublishedData;

namespace Opc.Ua.PubSub.Tests.PublishedData
{
    public class WriterGroupPublishedStateTests
    {

        /// <summary>
        /// PubSub message type mapping
        /// </summary>
        public enum PubSubMessageType
        {
            Uadp,
            Json
        }

        private const UInt16 NamespaceIndexAllTypes = 3;

        private const string UdpUrlFormat = "{0}://{1}:4840";
        private const string MqttUrlFormat = "{0}://{1}:1883";
        //private const string MqttsUrlFormat = "{0}://{1}:8883";

        [Test(Description = "Publish Uadp | Json DataSetMessages with KeyFrameCount and delta frames")]
        public void PublishDataSetMessages(
            [Values(PubSubMessageType.Uadp, PubSubMessageType.Json)]
                PubSubMessageType pubSubMessageType,
            [Values(1, 2, 3, 4)] Int32 keyFrameCount)
        {
            //Arrange
            object publisherId = 1;
            UInt16 writerGroupId = 1;

            string addressUrl = "http://localhost:1883";

            DataSetFieldContentMask dataSetFieldContentMask = DataSetFieldContentMask.None;

            DataSetMetaDataType[] dataSetMetaDataArray = new DataSetMetaDataType[]
            {
                MessagesHelper.CreateDataSetMetaData1("DataSet1"),
                MessagesHelper.CreateDataSetMetaData2("DataSet2"),
                MessagesHelper.CreateDataSetMetaData2("DataSet3"),
            };

            PubSubConfigurationDataType publisherConfiguration = null;

            if (pubSubMessageType == PubSubMessageType.Uadp)
            {
                UadpNetworkMessageContentMask uadpNetworkMessageContentMask = UadpNetworkMessageContentMask.PublisherId
                    | UadpNetworkMessageContentMask.WriterGroupId
                    | UadpNetworkMessageContentMask.PayloadHeader;
                UadpDataSetMessageContentMask uadpDataSetMessageContentMask = UadpDataSetMessageContentMask.None;

                publisherConfiguration = MessagesHelper.CreatePublisherConfiguration(
                    Profiles.PubSubMqttUadpTransport,
                    addressUrl, publisherId: publisherId, writerGroupId: writerGroupId,
                    uadpNetworkMessageContentMask: uadpNetworkMessageContentMask,
                    uadpDataSetMessageContentMask: uadpDataSetMessageContentMask,
                    dataSetFieldContentMask: dataSetFieldContentMask,
                    dataSetMetaDataArray: dataSetMetaDataArray, nameSpaceIndexForData: NamespaceIndexAllTypes,
                    keyFrameCount: Convert.ToUInt32(keyFrameCount));
            }

            if (pubSubMessageType == PubSubMessageType.Json)
            {
                JsonNetworkMessageContentMask jsonNetworkMessageContentMask = JsonNetworkMessageContentMask.NetworkMessageHeader
                    | JsonNetworkMessageContentMask.PublisherId
                    | JsonNetworkMessageContentMask.DataSetMessageHeader;
                JsonDataSetMessageContentMask jsonDataSetMessageContentMask = JsonDataSetMessageContentMask.DataSetWriterId;

                publisherConfiguration = MessagesHelper.CreatePublisherConfiguration(
                    Profiles.PubSubMqttJsonTransport,
                    addressUrl, publisherId: publisherId, writerGroupId: writerGroupId,
                    jsonNetworkMessageContentMask: jsonNetworkMessageContentMask,
                    jsonDataSetMessageContentMask: jsonDataSetMessageContentMask,
                    dataSetFieldContentMask: dataSetFieldContentMask,
                    dataSetMetaDataArray: dataSetMetaDataArray, nameSpaceIndexForData: NamespaceIndexAllTypes,
                    keyFrameCount: Convert.ToUInt32(keyFrameCount));
            }

            Assert.IsNotNull(publisherConfiguration, "publisherConfiguration should not be null");

            // Create publisher application for multiple datasets
            UaPubSubApplication publisherApplication = UaPubSubApplication.Create(publisherConfiguration);
            MessagesHelper.LoadData(publisherApplication, NamespaceIndexAllTypes);

            IUaPubSubConnection publisherConnection = publisherApplication.PubSubConnections.First();
            Assert.IsNotNull(publisherConnection, "Publisher first connection should not be null");

            Assert.IsNotNull(publisherConfiguration.Connections.First(), "publisherConfiguration first connection should not be null");
            Assert.IsNotNull(publisherConfiguration.Connections.First().WriterGroups.First(), "publisherConfiguration first writer group of first connection should not be null");

            WriterGroupPublishState state = new WriterGroupPublishState();
            var networkMessages = publisherConnection.CreateNetworkMessages(publisherConfiguration.Connections.First().WriterGroups.First(), state);
            Assert.IsNotNull(networkMessages, "connection.CreateNetworkMessages shall not return null");
            Assert.GreaterOrEqual(networkMessages.Count, 1, "connection.CreateNetworkMessages shall have at least one network message");

            object uaNetworkMessagesList = null;
            List<UaNetworkMessage> uaNetworkMessages = null;
            if (pubSubMessageType == PubSubMessageType.Uadp)
            {
                uaNetworkMessagesList = MessagesHelper.GetUaDataNetworkMessages(networkMessages.Cast<UadpNetworkMessage>().ToList());
                Assert.IsNotNull(uaNetworkMessagesList, "uaNetworkMessagesList shall not be null");
                uaNetworkMessages = ((IEnumerable<UaNetworkMessage>)uaNetworkMessagesList).Cast<UaNetworkMessage>().ToList();
            }
            if (pubSubMessageType == PubSubMessageType.Json)
            {
                uaNetworkMessagesList = MessagesHelper.GetUaDataNetworkMessages(networkMessages.Cast<JsonNetworkMessage>().ToList());
                uaNetworkMessages = ((IEnumerable<UaNetworkMessage>)uaNetworkMessagesList).Cast<UaNetworkMessage>().ToList();
            }
            Assert.IsNotNull(uaNetworkMessages, "uaNetworkMessages shall not be null. Data entry is missing from configuration!?");

            // get datastore data
            Dictionary<NodeId, DataValue> dataStoreData = new Dictionary<NodeId, DataValue>();
            foreach (UaNetworkMessage uaDataNetworkMessage in uaNetworkMessages)
            {
                Dictionary<NodeId, DataValue> dataSetsData = MessagesHelper.GetDataStoreData(publisherApplication, uaDataNetworkMessage, NamespaceIndexAllTypes);
                foreach (NodeId nodeId in dataSetsData.Keys)
                {
                    if (!dataStoreData.ContainsKey(nodeId))
                    {
                        dataStoreData.Add(nodeId, dataSetsData[nodeId]);
                    }
                }
            }
            Assert.IsNotEmpty(dataStoreData, "datastore entries should be greater than 0");

            // check if received data is valid
            foreach (UaNetworkMessage uaDataNetworkMessage in uaNetworkMessages)
            {
                ValidateDataSetMessageData(uaDataNetworkMessage, dataStoreData);
            }

            for (int keyCount = 0; keyCount < keyFrameCount - 1; keyCount++)
            {
                // change the values and get one more time the dataset(s) data
                MessagesHelper.UpdateSnapshotData(publisherApplication, NamespaceIndexAllTypes);
                networkMessages = publisherConnection.CreateNetworkMessages(publisherConfiguration.Connections.First().WriterGroups.First(), state);
                Assert.IsNotNull(networkMessages, "connection.CreateNetworkMessages shall not be null");
                Assert.GreaterOrEqual(networkMessages.Count, 1, "connection.CreateNetworkMessages shall have at least one network message");

                if (pubSubMessageType == PubSubMessageType.Uadp)
                {
                    uaNetworkMessagesList = MessagesHelper.GetUaDataNetworkMessages(networkMessages.Cast<UadpNetworkMessage>().ToList());
                    Assert.IsNotNull(uaNetworkMessagesList, "uaNetworkMessagesList shall not be null");
                    uaNetworkMessages = ((IEnumerable<UaNetworkMessage>)uaNetworkMessagesList).Cast<UaNetworkMessage>().ToList();
                }
                if (pubSubMessageType == PubSubMessageType.Json)
                {
                    uaNetworkMessagesList = MessagesHelper.GetUaDataNetworkMessages(networkMessages.Cast<JsonNetworkMessage>().ToList());
                    uaNetworkMessages = ((IEnumerable<UaNetworkMessage>)uaNetworkMessagesList).Cast<UaNetworkMessage>().ToList();
                }
                Assert.IsNotNull(uaNetworkMessages, "uaNetworkMessages shall not be null. Data entry is missing from configuration!?");
                
                // check if delta received data is valid
                Dictionary<NodeId, DataValue> snapshotData = MessagesHelper.GetSnapshotData(publisherApplication, NamespaceIndexAllTypes);
                foreach (UaNetworkMessage uaDataNetworkMessage in uaNetworkMessages)
                {
                    ValidateDataSetMessageData(uaDataNetworkMessage, keyFrameCount == 1 ? dataStoreData : snapshotData, keyFrameCount);
                }
            }
        }

        #region Private methods

        /// <summary>
        /// Validate dataset message data
        /// </summary>
        /// <param name="uaDataNetworkMessage"></param>
        /// <param name="keyFrameCount"></param>
        private void ValidateDataSetMessageData(UaNetworkMessage uaDataNetworkMessage, Dictionary<NodeId, DataValue> dataStoreData, Int32 keyFrameCount = 1)
        {
            foreach(UaDataSetMessage datasetMessage in uaDataNetworkMessage.DataSetMessages)
            {
                if(datasetMessage.DataSet.IsDeltaFrame)
                {
                    Assert.Greater(keyFrameCount, 1, "keyFrameCount > 1 if dataset is delta!");
                    foreach(Field field in datasetMessage.DataSet.Fields)
                    {
                        // for delta frames dataset might contains partial filled data
                        if(field == null)
                        {
                            continue;
                        }
                        NodeId targetNodeId = new NodeId(field.FieldMetaData.Name, NamespaceIndexAllTypes);
                        Assert.IsTrue(dataStoreData.ContainsKey(targetNodeId), "field name: '{0}' should be exists in partial received dataset", field.FieldMetaData.Name);
                        Assert.IsNotNull(dataStoreData[targetNodeId], "field: '{0}' should not be null", field.FieldMetaData.Name);
                        Assert.AreEqual(field.Value.Value, dataStoreData[targetNodeId].Value, "field: '{0}' value: {1} should be equal to datastore value: {2}",
                            field.FieldMetaData.Name, field.Value, dataStoreData[targetNodeId].Value);
                        // compare non values from state!?
                    }
                }
                else
                {
                    Assert.AreEqual(keyFrameCount, 1, "keyFrameCount = 1 if dataset is not delta!");
                    foreach (Field field in datasetMessage.DataSet.Fields)
                    {
                        Assert.IsNotNull(field, "field {0}: should not be null if dataset is not delta!", field.FieldMetaData.Name);
                        NodeId targetNodeId = new NodeId(field.FieldMetaData.Name, NamespaceIndexAllTypes);
                        Assert.IsTrue(dataStoreData.ContainsKey(targetNodeId), "field name: {0} should be exists in partial received dataset", field.FieldMetaData.Name);
                        Assert.IsNotNull(dataStoreData[targetNodeId], "field {0}: should not be null", field.FieldMetaData.Name);
                        Assert.AreEqual(field.Value.Value, dataStoreData[targetNodeId].Value, "field: '{0}' value: {1} should be equal to datastore value: {2}",
                            field.FieldMetaData.Name, field.Value, dataStoreData[targetNodeId].Value);
                    }
                }
            }
        }

        #endregion Private methods
    }
}
