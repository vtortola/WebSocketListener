using System;
using vtortola.WebSockets.Rfc6455;
using Xunit;

namespace vtortola.WebSockets.UnitTests
{
    public class WebSocketFrameHeaderTests
    {
        [Fact]
        public void CreateBigHeaderInt32()
        {
            var header = WebSocketFrameHeader.Create(int.MaxValue, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            Assert.Equal(10, header.HeaderLength);
            var buffer = new byte[10];
            header.ToBytes(buffer, 0);
            Assert.Equal(129, buffer[0]);
            Assert.Equal(127, buffer[1]);
            Assert.Equal(0, buffer[2]);
            Assert.Equal(0, buffer[3]);
            Assert.Equal(0, buffer[4]);
            Assert.Equal(0, buffer[5]);
            Assert.Equal(127, buffer[6]);
            Assert.Equal(255, buffer[7]);
            Assert.Equal(255, buffer[8]);
            Assert.Equal(255, buffer[9]);
        }

        [Fact]
        public void CreateBigHeaderInt64()
        {
            var header = WebSocketFrameHeader.Create(long.MaxValue, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            var buffer = new byte[10];
            header.ToBytes(buffer, 0);
            Assert.Equal(129, buffer[0]);
            Assert.Equal(127, buffer[1]);
            Assert.Equal(127, buffer[2]);
            Assert.Equal(255, buffer[3]);
            Assert.Equal(255, buffer[4]);
            Assert.Equal(255, buffer[5]);
            Assert.Equal(255, buffer[6]);
            Assert.Equal(255, buffer[7]);
            Assert.Equal(255, buffer[8]);
            Assert.Equal(255, buffer[9]);
        }

        [Fact]
        public void CreateBinaryFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Binary,
                new WebSocketExtensionFlags());
            var buffer = new byte[2];
            header.ToBytes(buffer, 0);
            Assert.Equal(130, buffer[0]);
            Assert.Equal(101, buffer[1]);
        }

        [Fact]
        public void CreateBinaryFrameHeaderWithExtensions()
        {
            var header = WebSocketFrameHeader.Create(101, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Binary,
                new WebSocketExtensionFlags
                {
                    Rsv1 = true, Rsv2 = true
                });
            var buffer = new byte[2];
            header.ToBytes(buffer, 0);
            Assert.Equal(226, buffer[0]);
            Assert.Equal(101, buffer[1]);
        }

        [Fact]
        public void CreateContinuationPartialFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, false, true, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            var buffer = new byte[2];
            header.ToBytes(buffer, 0);
            Assert.Equal(0, buffer[0]);
            Assert.Equal(101, buffer[1]);
        }

        [Fact]
        public void CreateFinalPartialFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, true, true, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            var buffer = new byte[2];
            header.ToBytes(buffer, 0);
            Assert.Equal(128, buffer[0]);
            Assert.Equal(101, buffer[1]);
        }

        [Fact]
        public void CreateMediumHeader()
        {
            var header = WebSocketFrameHeader.Create(138, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            Assert.Equal(4, header.HeaderLength);
            var buffer = new byte[4];
            header.ToBytes(buffer, 0);
            Assert.Equal(129, buffer[0]);
            Assert.Equal(126, buffer[1]);
            Assert.Equal(0, buffer[2]);
            Assert.Equal(138, buffer[3]);
        }

        [Fact]
        public void CreateMediumHeaderBiggerThanInt16()
        {
            ushort ilength = (ushort)short.MaxValue + 1;

            var header = WebSocketFrameHeader.Create(ilength, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            Assert.Equal(4, header.HeaderLength);
            var buffer = new byte[4];
            header.ToBytes(buffer, 0);
            Assert.Equal(129, buffer[0]);
            Assert.Equal(126, buffer[1]);
            Assert.Equal(128, buffer[2]);
            Assert.Equal(0, buffer[3]);
        }

        [Fact]
        public void CreateMediumMaxHeader()
        {
            var header = WebSocketFrameHeader.Create(ushort.MaxValue, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            Assert.Equal(4, header.HeaderLength);
            var buffer = new byte[4];
            header.ToBytes(buffer, 0);
            Assert.Equal(129, buffer[0]);
            Assert.Equal(126, buffer[1]);
            Assert.Equal(255, buffer[2]);
            Assert.Equal(255, buffer[3]);
        }
        [Fact]
        public void CreateSmallHeader()
        {
            var header = WebSocketFrameHeader.Create(101, true, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            var buffer = new byte[2];
            header.ToBytes(buffer, 0);
            Assert.Equal(129, buffer[0]);
            Assert.Equal(101, buffer[1]);
        }

        [Fact]
        public void CreateStartPartialFrameHeader()
        {
            var header = WebSocketFrameHeader.Create(101, false, false, default(ArraySegment<byte>), WebSocketFrameOption.Text,
                new WebSocketExtensionFlags());
            Assert.Equal(2, header.HeaderLength);
            var buffer = new byte[2];
            header.ToBytes(buffer, 0);
            Assert.Equal(1, buffer[0]);
            Assert.Equal(101, buffer[1]);
        }

        [Fact]
        public void ParseBigHeader()
        {
            var buffer = new byte[10];
            buffer[0] = 129;
            buffer[1] = 127;

            var length = BitConverter.GetBytes(long.MaxValue);
            Array.Reverse(length);
            length.CopyTo(buffer, 2);

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 10, new ArraySegment<byte>(new byte[4], 0, 4),
                out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.Equal(10, header.HeaderLength);
            Assert.Equal(long.MaxValue, header.ContentLength);
        }

        [Fact]
        public void ParseMediumHeader()
        {
            var buffer = new byte[6];
            buffer[0] = 129;
            buffer[1] = 126;

            ushort ilength = (ushort)short.MaxValue + 1;
            var length = BitConverter.GetBytes(ilength);
            Array.Reverse(length);
            length.CopyTo(buffer, 2);

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 4, new ArraySegment<byte>(new byte[4], 0, 4),
                out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.Equal(4, header.HeaderLength);
            Assert.Equal(ilength, header.ContentLength);
        }

        [Fact]
        public void ParseMediumMaxHeader()
        {
            var buffer = new byte[6];
            buffer[0] = 129;
            buffer[1] = 126;

            var ilength = ushort.MaxValue;
            var length = BitConverter.GetBytes(ilength);
            Array.Reverse(length);
            length.CopyTo(buffer, 2);

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 4, new ArraySegment<byte>(new byte[4], 0, 4),
                out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.Equal(4, header.HeaderLength);
            Assert.Equal(ilength, header.ContentLength);
        }

        [Fact]
        public void ParseSmallHeader()
        {
            var buffer = new byte[6];
            buffer[0] = 129;
            buffer[1] = 101;

            WebSocketFrameHeader header;
            Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 2, new ArraySegment<byte>(new byte[4], 0, 4),
                out header));
            Assert.NotNull(header);
            Assert.True(header.Flags.FIN);
            Assert.False(header.Flags.MASK);
            Assert.Equal(2, header.HeaderLength);
            Assert.Equal(101, header.ContentLength);
        }

        [Fact]
        public void FailToParseBigHeaderWhenOverflowsInt64()
        {
            var buffer = new byte[10];
            buffer[0] = 129;
            buffer[1] = 127;

            var ilength = (ulong)long.MaxValue + 1;
            var length = BitConverter.GetBytes(ilength);
            Array.Reverse(length);
            length.CopyTo(buffer, 2);

            Assert.Throws<WebSocketException>(() =>
            {
                WebSocketFrameHeader header;
                Assert.True(WebSocketFrameHeader.TryParse(buffer, 0, 10, new ArraySegment<byte>(new byte[4], 0, 4), out header));
                Assert.Equal(10, header.HeaderLength);
            });
        }
    }
}
