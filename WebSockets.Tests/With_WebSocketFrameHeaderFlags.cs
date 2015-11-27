using System;
using System.Collections;
using NUnit.Framework;
using vtortola.WebSockets.Rfc6455;

namespace WebSockets.Tests
{
    [TestFixture]
    public class With_WebSocketFrameHeaderFlags
    {
        [Test]
        public void With_WebSocketFrameHeaderFlags_Can_Parse()
        {
            Byte[] header = GenerateHeader(true, false, false, false, false, false, false,true, false);
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

        public static Byte[] GenerateHeader(Boolean fin, Boolean rsv1, Boolean rsv2, Boolean rsv3, Boolean opt4, Boolean opt3, Boolean opt2, Boolean opt1, Boolean mask)
        {
            Boolean[] header1 = new Boolean[8];
            header1[7] = fin;
            header1[6] = rsv1;
            header1[5] = rsv2;
            header1[4] = rsv3;
            header1[3] = opt4;
            header1[2] = opt3;
            header1[1] = opt2;
            header1[0] = opt1;

            Boolean[] header2 = new Boolean[8];
            header2[7] = mask;
            header2[6] = false;
            header2[5] = false;
            header2[4] = false;
            header2[3] = false;
            header2[2] = false;
            header2[1] = false;
            header2[0] = false;


            BitArray bitArray1 = new BitArray(header1);
            Byte[] byteHead = new Byte[2];
            bitArray1.CopyTo(byteHead, 0);

            BitArray bitArray2 = new BitArray(header2);
            bitArray2.CopyTo(byteHead, 1);

            return byteHead;
        }

    }
}
