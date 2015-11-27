using System;
using NUnit.Framework;
using vtortola.WebSockets;
using vtortola.WebSockets.Rfc6455;
using WebSocketException = System.Net.WebSockets.WebSocketException;

namespace WebSockets.Tests
{
    [TestFixture]
    public class With_WebSocketFrameHeader
    {
        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateSmallHeader()
        {
            var header = WebSocketFrameHeader.Create(101, true, false, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[2];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(129, buffer[0]);
            Assert.AreEqual(101, buffer[1]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateMediumHeader()
        {
            var header = WebSocketFrameHeader.Create(138, true, false, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[4];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(129, buffer[0]);
            Assert.AreEqual(126, buffer[1]);
            Assert.AreEqual(0, buffer[2]);
            Assert.AreEqual(138, buffer[3]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateMediumHeader_BiggerThanInt16()
        {
            UInt16 ilength = (UInt16)Int16.MaxValue + 1;

            var header = WebSocketFrameHeader.Create((Int64)ilength, true, false, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[4];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(129, buffer[0]);
            Assert.AreEqual(126, buffer[1]);
            Assert.AreEqual(128, buffer[2]);
            Assert.AreEqual(0, buffer[3]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateBigHeader_Int32()
        {
            var header = WebSocketFrameHeader.Create(Int32.MaxValue, true, false, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[10];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(129, buffer[0]);
            Assert.AreEqual(127, buffer[1]);
            Assert.AreEqual(0, buffer[2]);
            Assert.AreEqual(0, buffer[3]);
            Assert.AreEqual(0, buffer[4]);
            Assert.AreEqual(0, buffer[5]);
            Assert.AreEqual(127, buffer[6]);
            Assert.AreEqual(255, buffer[7]);
            Assert.AreEqual(255, buffer[8]);
            Assert.AreEqual(255, buffer[9]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateBigHeader_Int64()
        {
            var header = WebSocketFrameHeader.Create(Int64.MaxValue, true, false, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[10];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(129, buffer[0]);
            Assert.AreEqual(127, buffer[1]);
            Assert.AreEqual(127, buffer[2]);
            Assert.AreEqual(255, buffer[3]);
            Assert.AreEqual(255, buffer[4]);
            Assert.AreEqual(255, buffer[5]);
            Assert.AreEqual(255, buffer[6]);
            Assert.AreEqual(255, buffer[7]);
            Assert.AreEqual(255, buffer[8]);
            Assert.AreEqual(255, buffer[9]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateStartPartialFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, false, false, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[2];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(1, buffer[0]);
            Assert.AreEqual(101, buffer[1]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateContinuationPartialFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, false, true, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[2];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(0, buffer[0]);
            Assert.AreEqual(101, buffer[1]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateFinalPartialFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, true, true, WebSocketFrameOption.Text, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[2];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(128, buffer[0]);
            Assert.AreEqual(101, buffer[1]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateBinaryFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, true, false, WebSocketFrameOption.Binary, new WebSocketExtensionFlags());
            Byte[] buffer = new Byte[2];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(130, buffer[0]);
            Assert.AreEqual(101, buffer[1]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_CreateBinaryFrameHeader_WithExtensions()
        {
            var header = WebSocketFrameHeader.Create(101, true, false, WebSocketFrameOption.Binary, new WebSocketExtensionFlags() { Rsv1 = true, Rsv2 = true });
            Byte[] buffer = new Byte[2];
            header.ToBytes(buffer, 0);
            Assert.AreEqual(226, buffer[0]);
            Assert.AreEqual(101, buffer[1]);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_ParseSmallHeader()
        {
            Byte[] buffer = new Byte[6];
            buffer[0] = 129;
            buffer[1] = 101;

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 2, new ArraySegment<byte>(new Byte[4], 0, 4), out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.AreEqual((Int64)101, header.ContentLength);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_ParseMediumHeader()
        {
            Byte[] buffer = new Byte[6];
            buffer[0] = 129;
            buffer[1] = 126;

            UInt16 ilength = (UInt16)Int16.MaxValue + 1;
            var length = BitConverter.GetBytes(ilength);
            Array.Reverse(length);
            length.CopyTo(buffer, 2);

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 2, new ArraySegment<byte>(new Byte[4], 0, 4), out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.AreEqual((Int64)ilength, header.ContentLength);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_ParseBigHeader()
        {
            Byte[] buffer = new Byte[10];
            buffer[0] = 129;
            buffer[1] = 127;

            var length = BitConverter.GetBytes(Int64.MaxValue);
            Array.Reverse(length);
            length.CopyTo(buffer, 2);

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 2, new ArraySegment<byte>(new Byte[4], 0, 4), out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.AreEqual(Int64.MaxValue, header.ContentLength);
        }

        [Test]
        public void With_WebSocketFrameHeaderFlags_Fail_ParseBigHeader_When_Overflows_Int64()
        {
            Assert.Throws<WebSocketException>(() =>
            {
                Byte[] buffer = new Byte[10];
                buffer[0] = 129;
                buffer[1] = 127;

                UInt64 ilength = (UInt64)Int64.MaxValue + 1;
                var length = BitConverter.GetBytes(ilength);
                Array.Reverse(length);
                length.CopyTo(buffer, 2);

                WebSocketFrameHeader header;
                Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 2, new ArraySegment<byte>(new Byte[4], 0, 4),
                    out header));
            });
        }
    }
}
