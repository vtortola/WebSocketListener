using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using vtortola.WebSockets.Rfc6455;

namespace WebSocketListener.UnitTests
{
    [TestClass]
    public class With_Bytes
    {
        private void Check16(ushort value, bool isLittleEndian = true)
        {
            var bytes = BitConverter.GetBytes(value);
            var bb = new byte[10];
            if(isLittleEndian)
                Array.Reverse(bytes);

            Array.Copy(bytes, 0, bb, 2, 2);
            bytes = bb;

            var result = ByteArrayExtensions.ToUInt16(bytes, 2, isLittleEndian);
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void CanParse_UInt16()
        {
            Check16(0);
            Check16(1);
            Check16(ushort.MaxValue);
            Check16(ushort.MaxValue / 2);
            Check16(ushort.MaxValue / 3);
            Check16(ushort.MaxValue / 5);
            Check16(ushort.MaxValue / 7);
        }

        [TestMethod]
        public void CanParse_UInt16_Reversed()
        {
            Check16(0, false);
            Check16(1, false);
            Check16(ushort.MaxValue, false);
            Check16(ushort.MaxValue / 2, false);
            Check16(ushort.MaxValue / 3, false);
            Check16(ushort.MaxValue / 5, false);
            Check16(ushort.MaxValue / 7, false);
        }

        private void Check64(ulong value, bool isLittleEndian = true)
        {
            var bytes = BitConverter.GetBytes(value);
            if (isLittleEndian)
                Array.Reverse(bytes);
            var result = ByteArrayExtensions.ToUInt64(bytes, 0, isLittleEndian);
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void CanParse_UInt64()
        {
            Check64(0);
            Check64(1);
            Check64(ulong.MaxValue);
            Check64(ulong.MaxValue / 2);
            Check64(ulong.MaxValue / 3);
            Check64(ulong.MaxValue / 5);
            Check64(ulong.MaxValue / 7);
        }

        [TestMethod]
        public void CanParse_UInt64_Reversed()
        {
            Check64(0, false);
            Check64(1, false);
            Check64(ulong.MaxValue, false);
            Check64(ulong.MaxValue / 2, false);
            Check64(ulong.MaxValue / 3, false);
            Check64(ulong.MaxValue / 5, false);
            Check64(ulong.MaxValue / 7, false);
        }

    }
}
