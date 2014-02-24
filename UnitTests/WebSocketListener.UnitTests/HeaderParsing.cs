using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using vtortola.WebSockets;
using System.Collections;

namespace WebSocketListener.UnitTests
{
    [TestClass]
    public class HeaderParsing
    {
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

        [TestMethod]
        public void With_WebSocketFrameHeaderFlags_Parse()
        {
            Byte[] header = GenerateHeader(true, false, false, false, false, false, false,true, false);
            WebSocketFrameHeaderFlags flags = new WebSocketFrameHeaderFlags(header, 0);
            Assert.IsTrue(flags.FIN);
            Assert.IsTrue(flags.OPT1);
            Assert.IsTrue(flags.Option == WebSocketFrameOption.Text);

            Assert.IsFalse(flags.Option == WebSocketFrameOption.Binary);
            Assert.IsFalse(flags.MASK);

            header = GenerateHeader(false, false, false, false, false, false, false, true, true);

            flags = new WebSocketFrameHeaderFlags(header, 0);
            Assert.IsFalse(flags.FIN);
            Assert.IsTrue(flags.OPT1);
            Assert.IsTrue(flags.Option == WebSocketFrameOption.Text);

            Assert.IsFalse(flags.Option == WebSocketFrameOption.Binary);
            Assert.IsTrue(flags.MASK);

            header = GenerateHeader(false, false, false, false, false, false, true, false, true);

            flags = new WebSocketFrameHeaderFlags(header, 0);
            Assert.IsFalse(flags.FIN);
            Assert.IsFalse(flags.OPT1);
            Assert.IsFalse(flags.Option == WebSocketFrameOption.Text);
            Assert.IsTrue(flags.OPT2);
            Assert.IsTrue(flags.Option == WebSocketFrameOption.Binary);
            Assert.IsTrue(flags.MASK);

            header = GenerateHeader(true, false, false, false, true, false, false, true, true);

            flags = new WebSocketFrameHeaderFlags(header, 0);
            Assert.IsTrue(flags.FIN);
            Assert.IsTrue(flags.OPT1);
            Assert.IsFalse(flags.Option == WebSocketFrameOption.Text);
            Assert.IsFalse(flags.OPT2);
            Assert.IsFalse(flags.Option == WebSocketFrameOption.Binary);
            Assert.IsTrue(flags.MASK);
            Assert.IsFalse(flags.OPT3);
            Assert.IsTrue(flags.OPT4);
            Assert.IsTrue(flags.Option == WebSocketFrameOption.Ping);
        }
    }
}
