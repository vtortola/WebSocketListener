using System.Collections;
using vtortola.WebSockets.Rfc6455;
using Xunit;

namespace vtortola.WebSockets.UnitTests
{
    public class WebSocketFrameHeaderFlagsTests
    {
        public static byte[] GenerateHeader(bool fin, bool rsv1, bool rsv2, bool rsv3, bool opt4, bool opt3, bool opt2, bool opt1, bool mask)
        {
            var header1 = new bool[8];
            header1[7] = fin;
            header1[6] = rsv1;
            header1[5] = rsv2;
            header1[4] = rsv3;
            header1[3] = opt4;
            header1[2] = opt3;
            header1[1] = opt2;
            header1[0] = opt1;

            var header2 = new bool[8];
            header2[7] = mask;
            header2[6] = false;
            header2[5] = false;
            header2[4] = false;
            header2[3] = false;
            header2[2] = false;
            header2[1] = false;
            header2[0] = false;

            var bitArray1 = new BitArray(header1);
            var byteHead = new byte[2];
            bitArray1.CopyTo(byteHead, 0);

            var bitArray2 = new BitArray(header2);
            bitArray2.CopyTo(byteHead, 1);

            return byteHead;
        }

        [Fact]
        public void Parse()
        {
            var header = GenerateHeader(true, false, false, false, false, false, false, true, false);
            WebSocketFrameHeaderFlags flags;
            Assert.True(WebSocketFrameHeaderFlags.TryParse(header, 0, out flags));
            Assert.True(flags.FIN);
            Assert.True(flags.OPT1);
            Assert.True(flags.Option == WebSocketFrameOption.Text);

            Assert.False(flags.Option == WebSocketFrameOption.Binary);
            Assert.False(flags.MASK);

            header = GenerateHeader(false, false, false, false, false, false, false, true, true);

            Assert.True(WebSocketFrameHeaderFlags.TryParse(header, 0, out flags));
            Assert.False(flags.FIN);
            Assert.True(flags.OPT1);
            Assert.True(flags.Option == WebSocketFrameOption.Text);

            Assert.False(flags.Option == WebSocketFrameOption.Binary);
            Assert.True(flags.MASK);

            header = GenerateHeader(false, false, false, false, false, false, true, false, true);

            Assert.True(WebSocketFrameHeaderFlags.TryParse(header, 0, out flags));
            Assert.False(flags.FIN);
            Assert.False(flags.OPT1);
            Assert.False(flags.Option == WebSocketFrameOption.Text);
            Assert.True(flags.OPT2);
            Assert.True(flags.Option == WebSocketFrameOption.Binary);
            Assert.True(flags.MASK);

            header = GenerateHeader(true, false, false, false, true, false, false, true, true);

            Assert.True(WebSocketFrameHeaderFlags.TryParse(header, 0, out flags));
            Assert.True(flags.FIN);
            Assert.True(flags.OPT1);
            Assert.False(flags.Option == WebSocketFrameOption.Text);
            Assert.False(flags.OPT2);
            Assert.False(flags.Option == WebSocketFrameOption.Binary);
            Assert.True(flags.MASK);
            Assert.False(flags.OPT3);
            Assert.True(flags.OPT4);
            Assert.True(flags.Option == WebSocketFrameOption.Ping);
        }
    }
}
