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
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moq;
using NUnit.Framework;
using Opc.Ua.Bindings;
using Opc.Ua.Tests;

namespace Opc.Ua.Core.Tests.Stack.Transport
{
    /// <summary>
    /// A response that is larger than a message size limit must not be encoded
    /// in full before the limit is applied.
    /// </summary>
    [TestFixture]
    [Category("MessageSizeLimit")]
    public class MessageSizeLimitEncodingTests
    {
        private const int kMegabyte = 1024 * 1024;
        private const int kChannelLimit = 65536;

        /// <summary>
        /// The bytes of a message body that fit in one chunk without security.
        /// </summary>
        private static int PayloadSize(int bufferSize)
        {
            return bufferSize -
                TcpMessageLimits.SymmetricHeaderSize -
                TcpMessageLimits.SequenceHeaderSize -
                1;
        }

        [Test]
        public void ResponsePastTheEncoderLimitIsNotEncodedInFull()
        {
            // the channel accepts 16 MB, the encoder 64 KB.
            using var channel = new TestChannel(8192, 16 * kMegabyte, kChannelLimit);

            channel.SendResponse(1, CreateResponse(4 * kMegabyte));

            Assert.That(channel.Chunks, Has.Count.EqualTo(1));
            Assert.That(channel.ChunkType(0), Is.EqualTo(TcpMessageType.Final));
            Assert.That(
                channel.DecodeFault().ResponseHeader.ServiceResult,
                Is.EqualTo((StatusCode)StatusCodes.BadEncodingLimitsExceeded));

            // 64 KB are 9 chunks of 8 KB.
            Assert.That(channel.Pool.Peak, Is.LessThanOrEqualTo(12));
            Assert.That(channel.Pool.Outstanding, Is.Zero);
        }

        [Test]
        public void ResponsePastTheChannelLimitIsNotEncodedInFull()
        {
            // the channel accepts 64 KB in 8 chunks, the encoder has no limit.
            using var channel = new TestChannel(8192, kChannelLimit, 0);

            channel.SendResponse(1, CreateResponse(4 * kMegabyte));

            Assert.That(channel.Chunks, Has.Count.EqualTo(9));
            for (int ii = 0; ii < 8; ii++)
            {
                Assert.That(channel.ChunkType(ii), Is.EqualTo(TcpMessageType.Intermediate));
            }
            Assert.That(channel.ChunkType(8), Is.EqualTo(TcpMessageType.Abort));
            Assert.That(channel.AbortCode(8), Is.EqualTo(StatusCodes.BadResponseTooLarge));

            Assert.That(channel.Pool.Peak, Is.LessThanOrEqualTo(12));
            Assert.That(channel.Pool.Outstanding, Is.Zero);
        }

        [Test]
        public void ResponsePastBothLimitsIsStillAnsweredWithAFault()
        {
            // the channel accepts 64 KB, the encoder 1 MB.
            using var channel = new TestChannel(8192, kChannelLimit, kMegabyte);

            channel.SendResponse(1, CreateResponse(4 * kMegabyte));

            Assert.That(channel.Chunks, Has.Count.EqualTo(1));
            Assert.That(
                channel.DecodeFault().ResponseHeader.ServiceResult,
                Is.EqualTo((StatusCode)StatusCodes.BadEncodingLimitsExceeded));

            // 1 MB are 129 chunks of 8 KB.
            Assert.That(channel.Pool.Peak, Is.LessThanOrEqualTo(132));
            Assert.That(channel.Pool.Outstanding, Is.Zero);
        }

        /// <summary>
        /// The largest response a channel sends is not changed. With 8 KB chunks the
        /// chunk count is the limit that applies, with 16 KB chunks the message size.
        /// The size check of the channel lets a body pass that is up to the size of
        /// the chunk headers larger than the limit.
        /// </summary>
        [TestCase(8192, -1, true)]
        [TestCase(8192, 0, true)]
        [TestCase(8192, 64, false)]
        [TestCase(8192, 65536, false)]
        [TestCase(16384, -1, true)]
        [TestCase(16384, 0, true)]
        [TestCase(16384, 1, false)]
        [TestCase(16384, 65536, false)]
        public void LargestResponseOfAChannelIsUnchanged(int bufferSize, int delta, bool sent)
        {
            int payloadSize = PayloadSize(bufferSize);
            int largest = bufferSize == 8192
                ? 8 * payloadSize
                : kChannelLimit +
                    TcpMessageLimits.SymmetricHeaderSize +
                    TcpMessageLimits.SequenceHeaderSize;
            int bodySize = largest + delta;

            using var channel = new TestChannel(bufferSize, kChannelLimit, 0);
            ReadResponse response = CreateResponse(bodySize);

            channel.SendResponse(1, response);

            int last = channel.Chunks.Count - 1;
            if (sent)
            {
                Assert.That(channel.ChunkType(last), Is.EqualTo(TcpMessageType.Final));
                Assert.That(channel.BodySize(), Is.EqualTo(bodySize));
                Assert.That(
                    channel.DecodeResponse<ReadResponse>().Results,
                    Has.Count.EqualTo(response.Results.Count));
            }
            else
            {
                Assert.That(channel.ChunkType(last), Is.EqualTo(TcpMessageType.Abort));
                Assert.That(channel.AbortCode(last), Is.EqualTo(StatusCodes.BadResponseTooLarge));
            }
            // the chunks that are sent end with the first one that is over the limit.
            Assert.That(
                channel.Chunks,
                Has.Count.EqualTo(Math.Min(((bodySize - 1) / payloadSize) + 1, (largest / payloadSize) + 1)));
            Assert.That(channel.Pool.Outstanding, Is.Zero);
        }

        /// <summary>
        /// The largest response the encoder accepts is not changed.
        /// </summary>
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(8192, false)]
        public void LargestResponseOfTheEncoderIsUnchanged(int delta, bool sent)
        {
            int bodySize = kChannelLimit + delta;
            using var channel = new TestChannel(8192, 16 * kMegabyte, kChannelLimit);

            channel.SendResponse(1, CreateResponse(bodySize));

            if (sent)
            {
                Assert.That(channel.BodySize(), Is.EqualTo(bodySize));
                Assert.That(channel.DecodeResponse<ReadResponse>().Results, Is.Not.Empty);
            }
            else
            {
                Assert.That(channel.Chunks, Has.Count.EqualTo(1));
                Assert.That(
                    channel.DecodeFault().ResponseHeader.ServiceResult,
                    Is.EqualTo((StatusCode)StatusCodes.BadEncodingLimitsExceeded));
            }
            Assert.That(channel.Pool.Outstanding, Is.Zero);
        }

        /// <summary>
        /// Creates a response whose encoded body has exactly the size asked for.
        /// </summary>
        private static ReadResponse CreateResponse(int bodySize)
        {
            const int blockSize = 50000;
            var context = new ServiceMessageContext(NUnitTelemetryContext.Create())
            {
                MaxMessageSize = 0
            };

            var sizes = new List<int>();
            for (int left = bodySize; left > 0; left -= blockSize)
            {
                sizes.Add(Math.Min(left, blockSize));
            }

            // the last two blocks take up what the other fields of the response need.
            ReadResponse response = Create(sizes);
            int extra = BinaryEncoder.EncodeMessage(response, context).Length - bodySize;
            for (int ii = sizes.Count - 1; ii >= 0 && extra > 0; ii--)
            {
                int cut = Math.Min(extra, sizes[ii]);
                sizes[ii] -= cut;
                extra -= cut;
            }

            response = Create(sizes);
            Assert.That(BinaryEncoder.EncodeMessage(response, context), Has.Length.EqualTo(bodySize));
            return response;

            static ReadResponse Create(List<int> sizes)
            {
                var response = new ReadResponse();
                foreach (int size in sizes)
                {
                    response.Results.Add(new DataValue(new Variant(new byte[size])));
                }
                return response;
            }
        }

        /// <summary>
        /// A pool that counts the buffers that are rented at the same time.
        /// </summary>
        private sealed class TrackingPool : ArrayPool<byte>
        {
            public int Outstanding
            {
                get { lock (m_lock) { return m_buffers.Count; } }
            }

            public int Peak
            {
                get { lock (m_lock) { return m_peak; } }
            }

            public override byte[] Rent(int minimumLength)
            {
                byte[] buffer = new byte[Math.Max(256, minimumLength)];
                lock (m_lock)
                {
                    m_buffers.Add(buffer);
                    m_peak = Math.Max(m_peak, m_buffers.Count);
                }
                return buffer;
            }

            public override void Return(byte[] array, bool clearArray = false)
            {
                lock (m_lock)
                {
                    Assert.That(m_buffers.Remove(array), Is.True, "Buffer returned twice or not rented here.");
                }
            }

            private readonly object m_lock = new();
            private readonly HashSet<byte[]> m_buffers = [];
            private int m_peak;
        }

        /// <summary>
        /// An open server channel without security that records the chunks it sends.
        /// </summary>
        private sealed class TestChannel : TcpServerChannel
        {
            public TestChannel(int bufferSize, int channelLimit, int encoderLimit)
                : this(bufferSize, channelLimit, encoderLimit, NUnitTelemetryContext.Create())
            {
            }

            private TestChannel(
                int bufferSize,
                int channelLimit,
                int encoderLimit,
                ITelemetryContext telemetry)
                : base(
                    "test",
                    new Mock<ITcpChannelListener>().Object,
                    new BufferManager("test", bufferSize, telemetry),
                    new ChannelQuotas(new ServiceMessageContext(telemetry) { MaxMessageSize = encoderLimit })
                    {
                        MaxBufferSize = bufferSize,
                        MaxMessageSize = channelLimit
                    },
                    null,
                    [],
                    telemetry)
            {
                Pool = new TrackingPool();
                typeof(BufferManager)
                    .GetField("m_arrayPool", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(BufferManager, Pool);
                ChannelId = 1;
                UsedBySession = true;
                ChannelToken token = CreateToken();
                token.TokenId = 1;
                ActivateToken(token);
                State = TcpChannelState.Open;

                var socket = new Mock<IMessageSocket>();
                socket.Setup(s => s.MessageSocketEventArgs()).Returns(() =>
                {
                    var args = new Mock<IMessageSocketAsyncEventArgs>();
                    args.SetupAllProperties();
                    args.SetupGet(a => a.BytesTransferred)
                        .Returns(() => args.Object.BufferList?.TotalSize ?? 0);
                    return args.Object;
                });
                socket.Setup(s => s.Send(It.IsAny<IMessageSocketAsyncEventArgs>()))
                    .Callback<IMessageSocketAsyncEventArgs>(args =>
                    {
                        // copy before the write completes and the buffers are returned.
                        foreach (ArraySegment<byte> chunk in args.BufferList)
                        {
                            Chunks.Add([.. chunk]);
                        }
                    })
                    .Returns(false);
                Socket = socket.Object;
            }

            public TrackingPool Pool { get; }

            public List<byte[]> Chunks { get; } = [];

            public uint ChunkType(int index)
            {
                return BitConverter.ToUInt32(Chunks[index], 0) & TcpMessageType.ChunkTypeMask;
            }

            public uint AbortCode(int index)
            {
                return BitConverter.ToUInt32(Chunks[index], kHeaderSize);
            }

            public int BodySize()
            {
                return Chunks.Sum(chunk => chunk.Length - kHeaderSize);
            }

            public ServiceFault DecodeFault()
            {
                return DecodeResponse<ServiceFault>();
            }

            public T DecodeResponse<T>() where T : class, IEncodeable
            {
                var buffers = new BufferCollection();
                foreach (byte[] chunk in Chunks)
                {
                    buffers.Add(new ArraySegment<byte>(chunk, kHeaderSize, chunk.Length - kHeaderSize));
                }
                using var stream = new ArraySegmentStream(buffers);
                var context = new ServiceMessageContext(Quotas.MessageContext.Telemetry)
                {
                    MaxMessageSize = 0
                };
                IEncodeable message = BinaryDecoder.DecodeMessage(stream, null, context);
                Assert.That(message, Is.InstanceOf<T>());
                return (T)message;
            }

            private const int kHeaderSize =
                TcpMessageLimits.SymmetricHeaderSize + TcpMessageLimits.SequenceHeaderSize;
        }
    }
}
